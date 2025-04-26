using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    public class AAAFVGBase : QCAlgorithm
    {
        private Symbol _symbol;
        private decimal _stopLossPercentage = 0.01m; // Risk 1% per trade
        private decimal _riskRewardRatio = 2m;       // Minimum 1:2 risk-to-reward ratio

        // Session and key level trackers
        private decimal? _asianHigh = null;
        private decimal? _asianLow = null;
        private string _bias = null; // Can be "bullish", "bearish", or null. Consider using an enum for better type safety.
        private bool _liquidityGrabbed = false;
        private bool _secondaryRallyConfirmed = false;
        private (decimal Upper, decimal Lower)? _fvgZone = null; // Nullable ValueTuple for FVG
        private (decimal Fib62, decimal Fib79)? _oteZone = null; // Nullable ValueTuple for OTE
        private decimal? _previousDayHigh = null;
        private decimal? _previousDayLow = null;
        private decimal? _previousWeekHigh = null;
        private decimal? _previousWeekLow = null;

        // ATR for dynamic stop loss
        private AverageTrueRange _atr;

        // Session timing (using TimeSpan)
        private readonly TimeSpan _asianSessionEnd = new TimeSpan(5, 0, 0);  // End of Asian session
        private readonly TimeSpan _londonSessionStart = new TimeSpan(6, 0, 0); // Start for checking liquidity grab
        private readonly TimeSpan _londonSessionEnd = new TimeSpan(12, 0, 0); // End of London session
        private readonly TimeSpan _nyKillZoneStart = new TimeSpan(13, 0, 0); // NY Killzone start
        private readonly TimeSpan _nyKillZoneEnd = new TimeSpan(16, 0, 0);   // NY Killzone end

        // Order tracking
        private OrderTicket _activeOrderTicket = null;
        private OrderTicket _stopLossTicket = null;
        private OrderTicket _takeProfitTicket = null;

        public override void Initialize()
        {
            SetStartDate(2024 , 3 , 9);
            SetEndDate(2025, 4, 4);
            SetCash(100000);

            _symbol = AddForex("EURUSD", Resolution.Minute).Symbol;
            AddData<AAAMinute5>(_symbol);

            // Initialize ATR indicator
            _atr = ATR(_symbol, 14, resolution: Resolution.Hour);

            // Set Warmup period for indicators if necessary (ATR needs some bars)
            SetWarmUp(15, Resolution.Hour); // Warm up for ATR(14) hourly
        }

        public override void OnData(Slice data)
        {
            // Wait for warmup completion
            if (IsWarmingUp) return;


            TradeBar bar = data.Bars.First().Value;
            decimal price = bar.Close;
            TimeSpan currentTime = Time.TimeOfDay; // Get the time portion of the algorithm time

            // Update higher-timeframe levels at the start of the day or periodically
            // Doing it here captures the levels before the session logic starts
            if (Time.Date != StartDate.Date && currentTime < new TimeSpan(0, 5, 0)) // Update near start of day once
            {
                 UpdateHigherTimeframeLevels();
            }

            // 1. Track Asian session high/low (before Asian session ends)
            if (currentTime < _asianSessionEnd)
            {
                TrackAsianSessionHighLow(price);
                return; // Don't proceed with other logic during Asian session tracking
            }

            // Reset Asian levels if just passed the session end (to avoid using stale data if there was a gap)
            // This might be implicitly handled by the OnEndOfDay reset, but ensures clean state transition.
            // If needed: Add a flag `_asianSessionTrackedToday` reset in OnEndOfDay.

            // Ensure Asian levels were actually set
            if (_asianHigh == null || _asianLow == null)
            {
                 // Could happen if algorithm started mid-day or no data during Asian session
                 // Debug("Asian session levels not yet established.");
                 return;
            }

            // 2. Determine bias (Judas Swing / Turtle Soup) after Asian session, during London open
            if (!_liquidityGrabbed && currentTime >= _londonSessionStart && currentTime < _londonSessionEnd)
            {
                if (price < _asianLow.Value)
                {
                    _bias = "bearish"; // Price broke below Asian Low first -> Expect reversal upwards (Bullish actual intent) -> TARGET Asian High/Higher Prices
                    _liquidityGrabbed = true;
                    Debug($"{Time}: Turtle Soup below Asian low ({_asianLow.Value}) at {price}. Bias set to expect bullish move (targeting higher liquidity).");
                }
                else if (price > _asianHigh.Value)
                {
                    _bias = "bullish"; // Price broke above Asian High first -> Expect reversal downwards (Bearish actual intent) -> TARGET Asian Low/Lower Prices
                    _liquidityGrabbed = true;
                    Debug($"{Time}: Turtle Soup above Asian high ({_asianHigh.Value}) at {price}. Bias set to expect bearish move (targeting lower liquidity).");
                }
            }

             // 3. Confirm secondary rally (move towards opposite liquidity) in London session
             // This step seems counter-intuitive based on standard ICT names, re-check original logic.
             // The original python code checks if price moves *back* across the asian range *after* the initial liquidity grab.
             // Let's assume ConfirmSecondaryRally confirms the *intended* move direction is underway.
            if (_liquidityGrabbed && !_secondaryRallyConfirmed && currentTime >= _londonSessionStart && currentTime < _londonSessionEnd)
            {
                 // Let's redefine ConfirmSecondaryRally based on standard ICT ideas:
                 // After a grab below Asian Low (expecting bullish), we want to see price rally *above* some level (e.g., back above Asian Low or a recent high).
                 // After a grab above Asian High (expecting bearish), we want to see price drop *below* some level (e.g., back below Asian High or a recent low).
                 // We also need the London High/Low check.

                bool targetHit = CheckLondonSessionHighLow(price); // Check if an important opposing level was hit during London
                bool structureShifted = CheckMarketStructureShift(); // Check if recent structure confirms the expected move

                 // If liquidity was grabbed below (expecting bullish):
                 if (_bias == "bearish" && price > _asianLow.Value && structureShifted ) // Price moved back above Asian Low & MSS confirms up move
                 {
                     _secondaryRallyConfirmed = true; // Confirmation of bullish intent
                     Debug($"{Time}: Bullish secondary move confirmed after low grab. Price back above Asian Low ({_asianLow.Value}). Structure Shift: {structureShifted}. Target Hit: {targetHit}");
                 }
                 // If liquidity was grabbed above (expecting bearish):
                 else if (_bias == "bullish" && price < _asianHigh.Value && structureShifted ) // Price moved back below Asian High & MSS confirms down move
                 {
                      _secondaryRallyConfirmed = true; // Confirmation of bearish intent
                      Debug($"{Time}: Bearish secondary move confirmed after high grab. Price back below Asian High ({_asianHigh.Value}). Structure Shift: {structureShifted}. Target Hit: {targetHit}");
                 }
            }


            // 4. Detect FVG and OTE zones after secondary rally confirmation
            // Should ideally happen *during* the secondary move or shortly after confirmation
            if (_secondaryRallyConfirmed && _fvgZone == null) // Only detect once per confirmation
            {
                var candles = History(_symbol, 20, Resolution.Minute).ToList(); // Look back more bars for FVG/OTE context
                if (candles.Count > 3)
                {
                    _fvgZone = DetectFairValueGap(candles);
                    if (_fvgZone != null)
                    {
                        Debug($"{Time}: FVG detected: ({_fvgZone.Value.Upper}, {_fvgZone.Value.Lower})");
                    }

                    // Calculate OTE based on the swing created by the liquidity grab and subsequent move
                    // Need to define the swing points more clearly based on the grab and confirmation.
                    // Example: If bearish grab (expect bullish), OTE is on retracement from high formed after grab down to the low of the grab.
                    // Example: If bullish grab (expect bearish), OTE is on retracement from low formed after grab up to the high of the grab.
                    // This part needs refinement based on precise swing definition.
                    // Using Asian Range as proxy for initial calculation as per Python code:
                    decimal? swingHigh = candles.TakeLast(10).Max(c => (decimal?)c.High); // Recent swing high
                    decimal? swingLow = candles.TakeLast(10).Min(c => (decimal?)c.Low);  // Recent swing low

                    if (swingHigh.HasValue && swingLow.HasValue)
                    {
                         if (_bias == "bearish") // Expecting Bullish move, OTE is on pullback down
                         {
                            _oteZone = CalculateOTEZone(swingHigh.Value, _asianLow.Value); // OTE between recent high and Asian Low (grab point)
                            Debug($"{Time}: Bullish OTE calculated: ({_oteZone?.Fib62}, {_oteZone?.Fib79}) based on High {swingHigh.Value} and Asian Low {_asianLow.Value}");
                         }
                         else if (_bias == "bullish") // Expecting Bearish move, OTE is on pullback up
                         {
                            _oteZone = CalculateOTEZone(_asianHigh.Value, swingLow.Value); // OTE between Asian High (grab point) and recent low
                             Debug($"{Time}: Bearish OTE calculated: ({_oteZone?.Fib62}, {_oteZone?.Fib79}) based on Asian High {_asianHigh.Value} and Low {swingLow.Value}");
                         }
                    }
                }
            }

            // 5. Execute trades during NY Killzone if conditions align and not already invested
            if (currentTime >= _nyKillZoneStart && currentTime <= _nyKillZoneEnd && !Portfolio.Invested)
            {
                // Refined Confluence Check: Bias set, Liquidity Grabbed, Secondary Move Confirmed, Price is near OTE/FVG?
                if (ConfluenceCheck(price))
                {
                    PlaceTrade(price);
                }
            }
        }

        private void TrackAsianSessionHighLow(decimal price)
        {
            // Track high and low during Asian session
            if (_asianHigh == null || price > _asianHigh.Value)
            {
                _asianHigh = price;
            }
            if (_asianLow == null || price < _asianLow.Value)
            {
                _asianLow = price;
            }
            // Debug($"Tracking Asian Session: Low={_asianLow}, High={_asianHigh}");
        }

        private void UpdateHigherTimeframeLevels()
        {
             Debug($"{Time}: Updating daily/weekly levels...");
             // Update daily high/low levels (using 2 days to get previous complete day)
             var dailyHistory = History(_symbol, 2, Resolution.Daily);
             if (dailyHistory.Any())
             {
                 var previousDailyBar = dailyHistory.FirstOrDefault(); // First element is the previous day
                 if (previousDailyBar != null)
                 {
                     _previousDayHigh = previousDailyBar.High;
                     _previousDayLow = previousDailyBar.Low;
                     Debug($"Previous Day Levels: High={_previousDayHigh}, Low={_previousDayLow}");
                 }
             }

             // Update weekly high/low levels (using 6 days to ensure we get last week's range if today is Monday)
             // This logic assumes trading days. For full weeks, might need more bars or different logic.
             var weeklyHistory = History(_symbol, 6, Resolution.Daily);
             if (weeklyHistory.Count() > 1) // Need at least 2 bars to look at previous days
             {
                  // Exclude the current partial day if it exists
                 var previousWeekBars = weeklyHistory.Where(b => b.EndTime < Time.Date).ToList();
                 if (previousWeekBars.Any())
                 {
                    _previousWeekHigh = previousWeekBars.Max(b => b.High);
                    _previousWeekLow = previousWeekBars.Min(b => b.Low);
                    Debug($"Previous Week Levels: High={_previousWeekHigh}, Low={_previousWeekLow}");
                 }
             }
        }

        // Detects Fair Value Gaps (Imbalance) using a three-candle pattern
        private (decimal Upper, decimal Lower)? DetectFairValueGap(List<TradeBar> candles)
        {
            // Ensure we have enough candles
            if (candles.Count < 3) return null;

            // Iterate backwards from the second to last candle (index Count - 2)
            // to find the most recent FVG
            for (int i = candles.Count - 3; i >= 0; i--)
            {
                var candle1 = candles[i];     // First candle
                var candle2 = candles[i + 1]; // Middle candle (where the gap might be relative to)
                var candle3 = candles[i + 2]; // Third candle

                // Bullish FVG: Low of candle 3 is above High of candle 1
                if (candle3.Low > candle1.High)
                {
                    // Gap exists between candle 1 High and candle 3 Low
                    // The FVG zone is defined by these boundaries
                    return (candle1.High, candle3.Low); // (Upper bound = candle 3 Low, Lower bound = candle 1 High) -> No, FVG is the *gap*
                                                        // Corrected: FVG is the space from Candle 1 High up to Candle 3 Low
                                                        // Price might retrace *into* this zone.
                     // Let's define the zone as (candle1.High, candle3.Low) - the unfilled space.
                     return (candle1.High, candle3.Low);


                }
                // Bearish FVG: High of candle 3 is below Low of candle 1
                else if (candle3.High < candle1.Low)
                {
                     // Gap exists between candle 1 Low and candle 3 High
                     // The FVG zone is defined by these boundaries
                     // Corrected: FVG is the space from Candle 3 High up to Candle 1 Low.
                     // Let's define the zone as (candle3.High, candle1.Low) - the unfilled space.
                     return (candle3.High, candle1.Low);
                }
            }
            return null; // No FVG found
        }

        // Calculate Optimal Trade Entry (OTE) zone using Fibonacci retracement levels (62%, 79%)
        // Note: Standard ICT OTE often includes 70.5% as the midpoint (sweet spot)
        private (decimal Fib62, decimal Fib79)? CalculateOTEZone(decimal swingHigh, decimal swingLow)
        {
             // Ensure High > Low
            if (swingHigh <= swingLow) return null;

            decimal range = swingHigh - swingLow;
            decimal fib62, fib79;

            // If expecting a bullish move (buy entry), OTE is calculated on a down-swing retracement (High to Low)
            // If expecting a bearish move (sell entry), OTE is calculated on an up-swing retracement (Low to High)

             // The calculation here is for retracement *from* the high *downwards*.
             // Use this when looking for BUY entries in the OTE of a prior down move.
             // OTE Buy Zone:
             var fib62Buy = swingHigh - (0.618m * range); // Corrected to 61.8%
             var fib79Buy = swingHigh - (0.79m * range);

             // For SELL entries in the OTE of a prior UP move (swing low to swing high):
             var fib62Sell = swingLow + (0.618m * range);
             var fib79Sell = swingLow + (0.79m * range);

            // Return based on current bias (which determines the expected retracement direction)
            if (_bias == "bearish") // Bias bearish means we expect price to go UP, so we look for BUY OTE on pullback down
            {
                // OTE levels for buying are between Fib79 (lower) and Fib62 (higher) of the HIGH-to-LOW swing
                 return (fib62Buy, fib79Buy); // (Higher Level, Lower Level)
            }
            else if (_bias == "bullish") // Bias bullish means we expect price to go DOWN, so we look for SELL OTE on pullback up
            {
                // OTE levels for selling are between Fib62 (lower) and Fib79 (higher) of the LOW-to-HIGH swing
                 return (fib62Sell, fib79Sell); // (Lower Level, Higher Level)
            }

            return null; // Should not happen if bias is set
        }


        // Checks if price has hit key liquidity levels (like previous day/week H/L) during London
        // This helps confirm the intent behind the initial liquidity grab.
        private bool CheckLondonSessionHighLow(decimal price)
        {
            if (_bias == "bearish") // Initial move DOWN (turtle soup low), expect price to target HIGHS
            {
                bool hitPDH = _previousDayHigh.HasValue && price >= _previousDayHigh.Value;
                bool hitPWH = _previousWeekHigh.HasValue && price >= _previousWeekHigh.Value;
                // Could also check Asian High again
                bool hitAH = _asianHigh.HasValue && price >= _asianHigh.Value;
                return hitPDH || hitPWH || hitAH;
            }
            else if (_bias == "bullish") // Initial move UP (turtle soup high), expect price to target LOWS
            {
                bool hitPDL = _previousDayLow.HasValue && price <= _previousDayLow.Value;
                bool hitPWL = _previousWeekLow.HasValue && price <= _previousWeekLow.Value;
                 // Could also check Asian Low again
                bool hitAL = _asianLow.HasValue && price <= _asianLow.Value;
                return hitPDL || hitPWL || hitAL;
            }
            return false;
        }

        // Basic Market Structure Shift check
        // Looks for a break of a recent swing high (for bullish) or swing low (for bearish)
        private bool CheckMarketStructureShift()
        {
             int mssLookback = 15; // How many bars to look back for swings
             var recentCandles = History(_symbol, mssLookback, Resolution.Minute).ToList();
             if (recentCandles.Count < 5) return false; // Need enough bars to define structure

             // Find recent swing points (simplified: highest high / lowest low in recent periods)
             // A proper MSS involves breaking a *confirmed* swing high/low.
             // Simple version: Did price make a higher high after a low grab, or lower low after a high grab?

             // Find the index of the absolute high/low in the lookback period *excluding the last bar*
             int highIndex = -1;
             decimal highestHigh = decimal.MinValue;
             int lowIndex = -1;
             decimal lowestLow = decimal.MaxValue;

             for(int i=0; i < recentCandles.Count -1; i++) // Exclude last bar
             {
                 if(recentCandles[i].High > highestHigh) { highestHigh = recentCandles[i].High; highIndex = i; }
                 if(recentCandles[i].Low < lowestLow) { lowestLow = recentCandles[i].Low; lowIndex = i; }
             }


            if (_bias == "bearish") // Expecting Bullish move after low grab
            {
                 // Look for the most recent significant swing high *before* the current move up.
                 // Then check if the current price or recent high has broken above it.
                 // Simple check: Is the current high higher than the last swing high?
                 var lastHigh = recentCandles.Last().High;
                 if (highIndex != -1 && lastHigh > highestHigh)
                 {
                      // Debug($"Bullish MSS detected: Current high {lastHigh} broke recent swing high {highestHigh} at index {highIndex}");
                      return true;
                 }
            }
            else if (_bias == "bullish") // Expecting Bearish move after high grab
            {
                // Look for the most recent significant swing low *before* the current move down.
                 // Then check if the current price or recent low has broken below it.
                 var lastLow = recentCandles.Last().Low;
                 if(lowIndex != -1 && lastLow < lowestLow)
                 {
                    // Debug($"Bearish MSS detected: Current low {lastLow} broke recent swing low {lowestLow} at index {lowIndex}");
                    return true;
                 }
            }
            return false;
        }


         // Combined check for trade entry conditions during NY Killzone
        private bool ConfluenceCheck(decimal currentPrice)
        {
             // Basic checks first
             if (_bias == null || !_liquidityGrabbed || !_secondaryRallyConfirmed || _oteZone == null)
             {
                 return false;
             }

             // Check if price has retraced into the calculated OTE zone
             bool inOTEZone = false;
             if (_bias == "bearish") // Expecting Bullish Trade (Buy)
             {
                  // OTE Zone: (Fib62Buy, Fib79Buy) = (Higher Level, Lower Level)
                 inOTEZone = currentPrice <= _oteZone.Value.Fib62 && currentPrice >= _oteZone.Value.Fib79;
             }
             else if (_bias == "bullish") // Expecting Bearish Trade (Sell)
             {
                 // OTE Zone: (Fib62Sell, Fib79Sell) = (Lower Level, Higher Level)
                 inOTEZone = currentPrice >= _oteZone.Value.Fib62 && currentPrice <= _oteZone.Value.Fib79;
             }

             // Optional: Check if price is reacting from an FVG within or near the OTE
             bool nearFVG = false;
             if (_fvgZone != null)
             {
                 // Check if current price is within or very close to the FVG bounds
                 decimal fvgMid = (_fvgZone.Value.Upper + _fvgZone.Value.Lower) / 2m;
                 decimal tolerance = _atr.IsReady ? _atr.Current.Value * 0.1m : 0.0001m; // Small tolerance
                 nearFVG = Math.Abs(currentPrice - fvgMid) < tolerance || (currentPrice >= _fvgZone.Value.Lower && currentPrice <= _fvgZone.Value.Upper);
             }

              // Require price to be in the OTE zone for entry
              if (!inOTEZone) return false;

              // Add Market Structure Shift check again right before entry?
              bool structureOk = CheckMarketStructureShift();


              Debug($"{Time}: Confluence Check: Bias={_bias}, LiqGrab={_liquidityGrabbed}, SecRally={_secondaryRallyConfirmed}, InOTE={inOTEZone}, NearFVG={nearFVG}, StructOK={structureOk}");

              // Final condition: Must be in OTE and structure must support the move. FVG is optional confluence.
              return inOTEZone && structureOk;
        }


        private void PlaceTrade(decimal price)
        {
            if (!_atr.IsReady || _atr.Current.Value <= 0)
            {
                Debug("ATR not ready or invalid. Skipping trade.");
                return;
            }
            if (_oteZone == null || _bias == null) // Need OTE zone and bias for stop/target logic
            {
                Debug("OTE Zone or Bias not set. Skipping trade.");
                return;
            }

            var atrValue = _atr.Current.Value;
            decimal stopLossPrice;
            decimal takeProfitPrice;
            OrderDirection direction;

            if (_bias == "bearish") // Expecting Bullish move (Buy)
            {
                direction = OrderDirection.Buy;
                // Place stop slightly below the OTE zone (below 79% level) or below Asian Low
                stopLossPrice = Math.Min(_oteZone.Value.Fib79, _asianLow.Value) - atrValue * 0.5m; // Example: Half ATR below lowest support
                // Target could be Asian High, PDH, PWH, or fixed RR based on ATR
                // Using fixed RR based on ATR from entry price for simplicity matching python:
                takeProfitPrice = price + (atrValue * _riskRewardRatio);
                // More logical targets:
                // takeProfitPrice = Math.Max(_asianHigh.Value, _previousDayHigh ?? price);

            }
            else // Expecting Bearish move (Sell) (_bias == "bullish")
            {
                direction = OrderDirection.Sell;
                 // Place stop slightly above the OTE zone (above 79% level) or above Asian High
                stopLossPrice = Math.Max(_oteZone.Value.Fib79, _asianHigh.Value) + atrValue * 0.5m; // Example: Half ATR above highest resistance
                 // Target could be Asian Low, PDL, PWL, or fixed RR based on ATR
                 // Using fixed RR based on ATR from entry price for simplicity matching python:
                takeProfitPrice = price - (atrValue * _riskRewardRatio);
                 // More logical targets:
                 // takeProfitPrice = Math.Min(_asianLow.Value, _previousDayLow ?? price);
            }

            var stopLossDistance = Math.Abs(price - stopLossPrice);
            if (stopLossDistance <= 0)
            {
                Debug($"Stop loss distance is zero or negative ({stopLossDistance}). Skipping trade.");
                return;
            }

             // Calculate quantity using risk percentage and stop distance (Python's direct method)
            var portfolioValue = Portfolio.TotalPortfolioValue;
            if (portfolioValue <= 0 || stopLossDistance == 0)
            {
                Debug("Portfolio value zero or stop distance zero. Cannot calculate quantity.");
                return;
            }
             // Note: Forex requires considering pip value for accurate sizing. This is simplified.
            var quantity = Math.Floor((portfolioValue * _stopLossPercentage) / stopLossDistance);

             // Ensure quantity is adjusted for Forex minimums/multipliers if needed - QC usually handles this
             // quantity = Securities[_symbol].SymbolProperties.LotSize * Math.Floor(quantity / Securities[_symbol].SymbolProperties.LotSize);


            if (quantity <= 0)
            {
                Debug($"Calculated order quantity is zero or negative ({quantity}). Skipping trade.");
                return;
            }

            Debug($"Attempting Trade: {direction} {quantity} {_symbol.Value} at {price}. SL: {stopLossPrice}, TP: {takeProfitPrice}");

            // Place Market order with attached Stop Loss and Take Profit
            var parameters = new OrderProperties { TimeInForce = TimeInForce.Day }; // Example property
            var marketTicket = MarketOrder(_symbol, quantity * (direction == OrderDirection.Buy ? 1 : -1), asynchronous: false, tag: "Entry Order", orderProperties: parameters);

            if (marketTicket != null && (marketTicket.Status == OrderStatus.Filled || marketTicket.Status == OrderStatus.Submitted || marketTicket.Status == OrderStatus.PartiallyFilled))
            {
                 _activeOrderTicket = marketTicket;
                 // Place SL and TP - these modify the position, not the entry ticket directly in QC backtesting
                 _stopLossTicket = StopMarketOrder(_symbol, -marketTicket.Quantity, stopLossPrice, tag: "Stop Loss");
                 _takeProfitTicket = LimitOrder(_symbol, -marketTicket.Quantity, takeProfitPrice, tag: "Take Profit");

                 Debug($"Placed trade: {direction} {marketTicket.Quantity} {_symbol.Value} at ~{marketTicket.AverageFillPrice}. SL: {stopLossPrice}, TP: {takeProfitPrice}");
            }
            else
            {
                 Debug($"Market order failed to place or fill acceptably. Status: {marketTicket?.Status}");
            }
        }

        public override void OnEndOfDay()
        {
            // Reset all session-specific variables to prepare for the next day
            Debug($"{Time}: End of day reset.");
            _asianHigh = null;
            _asianLow = null;
            _bias = null;
            _liquidityGrabbed = false;
            _secondaryRallyConfirmed = false;
            _fvgZone = null;
            _oteZone = null;
            _activeOrderTicket = null;
            _stopLossTicket = null;
            _takeProfitTicket = null;

            // Optionally, update HTF levels here instead of start of day
             UpdateHigherTimeframeLevels();

            // Consider liquidating any open positions if it's strictly a day-trading strategy
             // Liquidate();
        }

         public override void OnOrderEvent(OrderEvent orderEvent)
        {
            // Optional: Log fills, cancellations, updates
            if (orderEvent.Status == OrderStatus.Filled || orderEvent.Status == OrderStatus.PartiallyFilled)
            {
                Debug($"{Time} - Order Filled: {orderEvent.Symbol} {orderEvent.FillQuantity} @ {orderEvent.FillPrice:F5} ({orderEvent.OrderId}, {orderEvent})");
                 // If the main entry order filled, maybe log the SL/TP association
                 // If SL or TP filled, reset state?
                 if (_activeOrderTicket != null && (orderEvent.OrderId == _stopLossTicket?.OrderId || orderEvent.OrderId == _takeProfitTicket?.OrderId) )
                 {
                    Debug($"{Time} - SL/TP Hit for order {_activeOrderTicket.OrderId}. Resetting active tickets.");
                    _activeOrderTicket = null; // Position closed
                    _stopLossTicket = null;
                    _takeProfitTicket = null;
                    // Resetting bias etc. should happen EOD, not on trade close, to allow for multiple trades per day if logic permits.
                 }
            }
             else if (orderEvent.Status == OrderStatus.Canceled || orderEvent.Status == OrderStatus.Invalid)
             {
                  Debug($"{Time} - Order Canceled/Invalid: {orderEvent.Symbol} ({orderEvent.OrderId}, {orderEvent}) - Reason: {orderEvent.Message}");
             }
        }
    }
}
