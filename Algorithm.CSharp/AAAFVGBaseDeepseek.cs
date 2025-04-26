using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class AAAFVGBaseDeepseek : QCAlgorithm
    {
        private Symbol _symbol;
        private decimal _stopLossPercentage = 0.01m;
        private int _riskRewardRatio = 2;
        private decimal? _asianHigh;
        private decimal? _asianLow;
        private string _bias;
        private bool _liquidityGrabbed;
        private bool _secondaryRallyConfirmed;
        private Tuple<decimal, decimal> _fvgZone;
        private Tuple<decimal, decimal> _oteZone;
        private decimal _previousDayHigh;
        private decimal _previousDayLow;
        private decimal _previousWeekHigh;
        private decimal _previousWeekLow;
        private AverageTrueRange _atr;
        private readonly TimeSpan _asianSessionEnd = new TimeSpan(5, 0, 0);
        private readonly TimeSpan _londonSessionEnd = new TimeSpan(12, 0, 0);
        private readonly TimeSpan _nyKillZoneStart = new TimeSpan(13, 0, 0);
        private readonly TimeSpan _nyKillZoneEnd = new TimeSpan(16, 0, 0);
        private OrderTicket _activeOrderTicket;
        private OrderTicket _stopLossTicket;
        private OrderTicket _takeProfitTicket;

        public override void Initialize()
        {
            SetStartDate(2025, 1, 1);
            SetEndDate(2025, 4, 4);
            SetCash(100000);
            _symbol = AddCfd("XAUUSD", Resolution.Minute).Symbol;
            AddData<AAAMinute5>(_symbol);
            AddData<AAAHour>(_symbol);
            AddData<AAADaily>(_symbol);
            _atr = ATR(_symbol, 14, MovingAverageType.Simple, Resolution.Hour);
        }

        public override void OnData(Slice data)
        {
            TradeBar bar = data.Bars.First().Value;
            Log($"Time: {bar.Time}, Open: {bar.Open}, High: {bar.High}, Low: {bar.Low}, Close: {bar.Close}, Volume: {bar.Volume}");
            var price = bar.Close;
            
            Securities[_symbol].SetMarketPrice(new Tick{ Value = price });
            var currentTime = Time.TimeOfDay;

            UpdateHigherTimeframeLevels();

            if (currentTime < _asianSessionEnd)
            {
                TrackAsianSessionHighLow(price);
                return;
            }

            if (!_liquidityGrabbed && _asianHigh.HasValue && _asianLow.HasValue &&
                currentTime >= new TimeSpan(6, 0, 0) && currentTime < _londonSessionEnd)
            {
                if (price < _asianLow.Value)
                {
                    _bias = "bearish";
                    _liquidityGrabbed = true;
                    Debug($"Turtle Soup below Asian low at {price}. Bias set to bearish.");
                }
                else if (price > _asianHigh.Value)
                {
                    _bias = "bullish";
                    _liquidityGrabbed = true;
                    Debug($"Turtle Soup above Asian high at {price}. Bias set to bullish.");
                }
            }

            if (_liquidityGrabbed && !_secondaryRallyConfirmed &&
                currentTime >= new TimeSpan(6, 0, 0) && currentTime < _londonSessionEnd)
            {
                if (ConfirmSecondaryRally(price) && CheckLondonSessionHighLow(price))
                {
                    _secondaryRallyConfirmed = true;
                    Debug($"Secondary rally confirmed at {price}. Important level targeted.");
                }
            }

            if (_secondaryRallyConfirmed && _fvgZone == null)
            {
                var candles = History<AAAMinute5>(_symbol, 10, Resolution.Minute);
                //convert quote bar to trade bar 
                List<TradeBar> tradeBars = candles.Select(qb => new TradeBar
                {
                    Time = qb.Time,
                    Open = qb.Open,
                    High = qb.High,
                    Low = qb.Low,
                    Close = qb.Close,
                    Volume = qb.Value
                }).ToList();
                _fvgZone = DetectFairValueGap(tradeBars);
                if (_fvgZone != null)
                    Debug($"FVG detected: {_fvgZone.Item1} - {_fvgZone.Item2}");

                if (_bias == "bullish")
                {
                    var high = candles.Max(c => c.High);
                    _oteZone = CalculateOTEZone(high, _asianLow.Value);
                }
                else if (_bias == "bearish")
                {
                    var low = candles.Min(c => c.Low);
                    _oteZone = CalculateOTEZone(_asianHigh.Value, low);
                }
            }

            if (currentTime >= _nyKillZoneStart && currentTime <= _nyKillZoneEnd &&
                !Portfolio.Invested)
            {
                var candles = History<AAAMinute5>(_symbol, 10, Resolution.Minute);
                //convert quote bar to trade bar
                List<TradeBar> tradeBars = candles.Select(qb => new TradeBar
                {
                    Time = qb.Time,
                    Open = qb.Open,
                    High = qb.High,
                    Low = qb.Low,
                    Close = qb.Close,
                    Volume = qb.Value
                }).ToList();
                if (ConfluenceCheck(price, tradeBars))
                    PlaceTrade(price);
            }
        }

        private void TrackAsianSessionHighLow(decimal price)
        {
            if (!_asianHigh.HasValue || price > _asianHigh.Value)
                _asianHigh = price;
            if (!_asianLow.HasValue || price < _asianLow.Value)
                _asianLow = price;
        }

        private void UpdateHigherTimeframeLevels()
        {
            var dailyHistory = History<AAADaily>(_symbol, 1, Resolution.Daily);
            if (dailyHistory.Any())
            {
                var lastDaily = dailyHistory.Last();
                _previousDayHigh = lastDaily.High;
                _previousDayLow = lastDaily.Low;
            }

            var weeklyHistory = History<AAADaily>(_symbol, 5, Resolution.Daily);
            if (weeklyHistory.Any())
            {
                _previousWeekHigh = weeklyHistory.Max(qb => qb.High);
                _previousWeekLow = weeklyHistory.Min(qb => qb.Low);
            }
        }

        private Tuple<decimal, decimal> DetectFairValueGap(IEnumerable<TradeBar> candles)
        {
            var list = candles.ToList();
            if (list.Count < 3) return null;

            for (var i = 0; i < list.Count - 2; i++)
            {
                var candle1 = list[i];
                var candle2 = list[i + 1];
                var candle3 = list[i + 2];

                if (candle1.High < candle2.Low)
                {
                    if (candle3.Low > candle1.High)
                        return Tuple.Create(candle1.High, candle2.Low);
                }
                else if (candle1.Low > candle2.High)
                {
                    if (candle3.High < candle1.Low)
                        return Tuple.Create(candle2.High, candle1.Low);
                }
            }
            return null;
        }

        private Tuple<decimal, decimal> CalculateOTEZone(decimal swingHigh, decimal swingLow)
        {
            var fib62 = swingHigh - 0.62m * (swingHigh - swingLow);
            var fib79 = swingHigh - 0.79m * (swingHigh - swingLow);
            return Tuple.Create(fib62, fib79);
        }

        private bool ConfirmSecondaryRally(decimal price)
        {
            return _bias switch
            {
                "bullish" => price < _asianLow.Value,
                "bearish" => price > _asianHigh.Value,
                _ => false
            };
        }

        private bool CheckLondonSessionHighLow(decimal price)
        {
            return _bias switch
            {
                "bullish" => price < _previousDayLow || price < _previousWeekLow,
                "bearish" => price > _previousDayHigh || price > _previousWeekHigh,
                _ => false
            };
        }

        private bool ConfluenceCheck(decimal price, IEnumerable<TradeBar> candles)
        {
            return _liquidityGrabbed &&
                   _secondaryRallyConfirmed &&
                   CheckLondonSessionHighLow(price) &&
                   CheckMarketStructureShift();
        }

        private bool CheckMarketStructureShift()
        {
            var recentCandles = History<AAAMinute5>(_symbol, 10, Resolution.Minute).ToList();
            if (recentCandles.Count < 3) return false;

            return _bias switch
            {
                "bullish" => recentCandles[^2].Low > recentCandles[^3].Low &&
                             recentCandles.Last().High > recentCandles[^2].High,
                "bearish" => recentCandles[^2].High < recentCandles[^3].High &&
                             recentCandles.Last().Low < recentCandles[^2].Low,
                _ => false
            };
        }

        private void PlaceTrade(decimal price)
        {
            if (!_atr.IsReady)
            {
                Debug("ATR is not ready. Skipping trade.");
                return;
            }

            var atrValue = _atr.Current.Value;
            if (atrValue <= 0)
            {
                Debug("ATR value is invalid. Skipping trade.");
                return;
            }

            if (_oteZone == null)
            {
                Debug("OTE Zone not calculated. Skipping trade.");
                return;
            }

            var stopLoss = _bias == "bullish" ? _oteZone.Item1 : _oteZone.Item2;
            var takeProfit = _bias == "bullish"
                ? price + atrValue * _riskRewardRatio
                : price - atrValue * _riskRewardRatio;

            var riskPerShare = Math.Abs(price - stopLoss);
            if (riskPerShare == 0)
            {
                Debug("Risk per share is zero. Skipping trade.");
                return;
            }

            var quantity = Portfolio.TotalPortfolioValue * _stopLossPercentage / riskPerShare;
            quantity = Math.Round(quantity, 2);

            if (quantity <= 0)
            {
                Debug("Invalid quantity. Skipping trade.");
                return;
            }

            var orderQuantity = (int)(_bias == "bullish" ? quantity : -quantity);
            _activeOrderTicket = MarketOrder(_symbol, orderQuantity);

            var stopQuantity = -orderQuantity;
            _stopLossTicket = StopMarketOrder(_symbol, stopQuantity, stopLoss, "Stop Loss");

            var takeProfitQuantity = -orderQuantity;
            _takeProfitTicket = LimitOrder(_symbol, takeProfitQuantity, takeProfit, "Take Profit");

            Debug($"Placed {_bias} trade: {orderQuantity} units at {price}. SL: {stopLoss}, TP: {takeProfit}");
        }
    }
}
