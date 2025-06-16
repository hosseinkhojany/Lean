using QuantConnect.Indicators;
using QuantConnect.Data.Market;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Parameters; // Required for [Parameter] and Color

namespace QuantConnect.Algorithm.CSharp;

class AAAOBGemini : QCAlgorithm
{
    private Symbol _xauusdSymbol;
    private OBIndicator _obIndicator;
    public override void Initialize()
    {
        SetStartDate(2025, 1, 1); 
        SetEndDate(2025, 4, 4);
        SetCash(100000);

        Settings.DailyPreciseEndTime = false;
        _xauusdSymbol = AddData<AAAMinute>("XAUUSD", Resolution.Minute).Symbol;

            
        var history3 = History<AAAMinute>(new[] { _xauusdSymbol }, 1000, Resolution.Minute).ToList();
        int i = HistoryProvider.DataPointCount;
        Log($"History Size: {history3.Count} DataPointCount: {i}");
        
        _obIndicator = new OBIndicator("OBIndicator");
        
        SetWarmup(10, Resolution.Minute);
    }

    public override void OnData(Slice slice)
    {
  
        var currentBar = slice.Get<AAAMinute>().First().Value;
        if (currentBar.Price == 0)
        {
            Log($"Price is zero for {_xauusdSymbol} at {Time}");
            return;
        }

        TradeBar tradeBar = new TradeBar
        {
            Time = currentBar.Time,
            Open = currentBar.Open,
            High = currentBar.High,
            Low = currentBar.Low,
            Close = currentBar.Close,
            Volume = currentBar.Volume,
            Symbol = _xauusdSymbol
        };
        _obIndicator.Update(tradeBar);
        if (_obIndicator.IsReady)
        {
            var signal = _obIndicator.Current.Value;
            if (signal == 1)
            {
                LimitOrder(_xauusdSymbol, 1, currentBar.Close + 10);
                Log($"{Time}: Data confirmed for {_xauusdSymbol}. Price: {currentBar.Price}. Placing Market Order.");
            }
            else if (signal == -1)
            {
                LimitOrder(_xauusdSymbol, -1, currentBar.Close - 10);
                Log($"{Time}: Data confirmed for {_xauusdSymbol}. Price: {currentBar.Price}. Placing Market Order.");
            }
        }
        else
        {
            Log($"OBIndicator is not ready yet.");
        }
        
    }
    
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log($"{Time}: Order Event: {orderEvent.Symbol}, Status: {orderEvent.Status}, Qty: {orderEvent.Quantity}, FillQty: {orderEvent.FillQuantity}, FillPrice: {orderEvent.FillPrice:F5}");
        }

        public override void OnEndOfAlgorithm()
        {
            Log($"End of Algorithm. Final Portfolio Value: {Portfolio.TotalPortfolioValue:C}");
        }
        

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public List<Language> Languages { get; } = new() { Language.CSharp, Language.Python };

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public long DataPoints => 9999;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public int AlgorithmHistoryDataPoints => 9999;

        /// <summary>
        /// Final status of the algorithm
        /// </summary>
        public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Orders", "0"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "0%"},
            {"Drawdown", "0%"},
            {"Expectancy", "0"},
            {"Start Equity", "100000"},
            {"End Equity", "100000"},
            {"Net Profit", "0%"},
            {"Sharpe Ratio", "0"},
            {"Sortino Ratio", "0"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "0"},
            {"Tracking Error", "0"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$0.00"},
            {"Estimated Strategy Capacity", "$0"},
            {"Lowest Capacity Asset", ""},
            {"Portfolio Turnover", "0%"},
            {"OrderListHash", "d41d8cd98f00b204e9800998ecf8427e"}
        };
}

public class OBIndicator : BarIndicator, IIndicatorWarmUpPeriodProvider
{
    // --- Parameters ---
    [Parameter("Show Last Order Blocks")]
    public bool ShowLast { get; set; } = true;

    [Parameter("Number of Last OBs to Show")]
    public int NumBlocksToShow { get; set; } = 5;

    [Parameter("Bullish OB Color")]
    public Color BullishColor { get; set; } = Color.FromArgb(90, 8, 153, 129); // #089981 with 90 alpha

    [Parameter("Bearish OB Color")]
    public Color BearishColor { get; set; } = Color.FromArgb(90, 242, 54, 69); // #f23645 with 90 alpha

    // Input Group 2 (Activity - not directly used in OB logic but kept for reference)
    [Parameter("Show Buy/Sell Activity")]
    public bool ShowActivity { get; set; } = true;
    [Parameter("Activity Up Color")]
    public Color ActivityUpColor { get; set; } = Color.FromArgb(50, 8, 153, 129);
    [Parameter("Activity Down Color")]
    public Color ActivityDownColor { get; set; } = Color.FromArgb(50, 242, 54, 69);

    // Input Group 3 (Construction)
    public enum ObConstructionMode { Length, Full }
    [Parameter("Construction Mode")]
    public ObConstructionMode ConstructionMode { get; set; } = ObConstructionMode.Length;

    [Parameter("Lookback Length")]
    public int Length { get; set; } = 5;

    public enum ObMitigationMethod { Close, Wick, Avg }
    [Parameter("Mitigation Method")]
    public ObMitigationMethod MitigationMethod { get; set; } = ObMitigationMethod.Close;

    // Metrics and Lines (Parameters controlling display aspects handled by plotting logic)
    [Parameter("Metric Size (Visual Only)")]
    public string MetricSize { get; set; } = "Normal"; // String enums not directly supported for [Parameter], use string

    [Parameter("Show Metrics (Visual Only)")]
    public bool ShowMetrics { get; set; } = true;

    [Parameter("Show Mid-Line (Visual Only)")]
    public bool ShowMidLine { get; set; } = true;

    [Parameter("Hide Overlapping OBs")]
    public bool HideOverlap { get; set; } = true;

    // --- Indicators ---
    private readonly Maximum _high;
    private readonly Minimum _low;
    private readonly AverageTrueRange _atr;
    // Using PivotHigh on High price. Change to Volume if custom implementation is available.
    private readonly PivotPointsHighLow _pivotHigh;
    private readonly Identity _identityHigh; // To access historical highs
    private readonly Identity _identityLow;  // To access historical lows
    private readonly Identity _identityOpen; // To access historical opens
    private readonly Identity _identityClose; // To access historical closes
    private readonly Identity _identityVolume;// To access historical volumes

    // --- State Variables ---
    private int _direction = 0;
    private DateTime _previousBarTime = DateTime.MinValue;
    private TimeSpan _barTimeSpan = TimeSpan.Zero;

    // --- Order Block Lists ---
    // Using List for simplicity. LinkedList might be slightly more performant for frequent Insert(0).
    public List<OrderBlock> BullishBlocks { get; } = new List<OrderBlock>();
    public List<OrderBlock> BearishBlocks { get; } = new List<OrderBlock>();

    // Public property to expose the *current* signal (optional)
    // 1 = Price entered top bullish OB, -1 = Price entered bottom bearish OB, 0 = No entry
    public int Signal { get; private set; }

    public OBIndicator(string name, int length = 5,
                            ObConstructionMode constructionMode = ObConstructionMode.Length,
                            ObMitigationMethod mitigationMethod = ObMitigationMethod.Close,
                            bool hideOverlap = true,
                            Color? bullishColor = null, Color? bearishColor = null)
        : base(name)
    {
        Length = length;
        ConstructionMode = constructionMode;
        MitigationMethod = mitigationMethod;
        HideOverlap = hideOverlap;
        // Use provided colors or defaults
        BullishColor = bullishColor ?? Color.FromArgb(90, 8, 153, 129);
        BearishColor = bearishColor ?? Color.FromArgb(90, 242, 54, 69);

        // Ensure Length is at least 1
        if (Length < 1) Length = 1;

        // Initialize indicators - Need lookback period + 1 for comparisons like high[len] vs high
        // PivotHigh needs left and right strength (len, len in PineScript)
        _high = new Maximum(name + "_High", Length + 1);
        _low = new Minimum(name + "_Low", Length + 1);
        _atr = new AverageTrueRange(name + "_ATR", Length, MovingAverageType.Simple); // Pine default ATR is RMA/SMMA, using Simple here
        _pivotHigh = new PivotPointsHighLow(name + "_PivotHigh", Length, Length); // Assuming Price Pivot
        _identityHigh = new Identity(name + "_IdH");
        _identityLow = new Identity(name + "_IdL");
        _identityOpen = new Identity(name + "_IdO");
        _identityClose = new Identity(name + "_IdC");
        _identityVolume = new Identity(name + "_IdV");

        // Warm up period ensures indicators have enough data
        // Need at least Length bars for ATR/High/Low lookbacks, plus Length for PivotHigh history
        WarmUpPeriod = Length + Length + 1;
    }

    protected override decimal ComputeNextValue(IBaseDataBar input)
    {
        var bar = input as TradeBar;
        if (bar == null)
        {
             // This indicator requires TradeBar data (OHLCV)
             return 0; // Or throw exception
        }

        // Update helper identities and indicators
        _identityHigh.Update(input.Time, bar.High);
        _identityLow.Update(input.Time, bar.Low);
        _identityOpen.Update(input.Time, bar.Open);
        _identityClose.Update(input.Time, bar.Close);
        _identityVolume.Update(input.Time, bar.Volume);

        _high.Update(input.Time, bar.High);
        _low.Update(input.Time, bar.Low);
        _atr.Update(bar);
        _pivotHigh.Update(bar); // Update PivotHigh with High price

         if (_previousBarTime != DateTime.MinValue)
        {
            _barTimeSpan = input.Time - _previousBarTime;
        }
        _previousBarTime = input.Time;


        // --- Check if Indicators are Ready ---
        if (!IsReady || !CurrentBarHasEnoughHistory(Length))
        {
            return 0; // Not enough data yet
        }

        // --- Determine Direction ---
        // Pine: b.h[len] > up ? -1 : b.l[len] < dn ? 1 : dir[1]
        // up = ta.highest(len) -> _high[1] (highest of last len bars, *excluding* current)
        // dn = ta.lowest(len)  -> _low[1]  (lowest of last len bars, *excluding* current)
        // b.h[len] -> _identityHigh[Length]
        // b.l[len] -> _identityLow[Length]
        decimal highestLenBarsAgo = _high[1]; // Highest high over previous 'Length' bars
        decimal lowestLenBarsAgo = _low[1];   // Lowest low over previous 'Length' bars
        decimal highLenBarsAgo = _identityHigh[Length];
        decimal lowLenBarsAgo = _identityLow[Length];

        if (highLenBarsAgo > highestLenBarsAgo)
        {
            _direction = -1; // Bearish trend
        }
        else if (lowLenBarsAgo < lowestLenBarsAgo)
        {
            _direction = 1; // Bullish trend
        }
        // else: direction remains the same (_direction = _direction;)

        // --- Check for Pivot and Create Order Blocks ---
        // Pine: pv = ta.pivothigh(b.v, len, len)
        // We are using price pivot: _pivotHigh.IsReady && _pivotHigh.Current.Value != 0m
        // Note: Lean PivotHigh might signal *after* the actual pivot bar. Check behavior.
        // PineScript pivot occurs when high[len] is the highest.
        bool pivotDetected = false;
        if (CurrentBarHasEnoughHistory(Length + Length)) // Need more history for reliable pivot comparison
        {
            // Check if the high 'Length' bars ago was the highest in the 2*Length+1 window
             decimal pivotHighValue = _identityHigh[Length];
             bool isHighest = true;
             for (int i = 0; i <= 2 * Length; i++)
             {
                if (i != Length && CurrentBarHasEnoughHistory(i)) // Check if index exists
                {
                    if (_identityHigh[i] > pivotHighValue)
                    {
                        isHighest = false;
                        break;
                    }
                }
             }
             pivotDetected = isHighest;
        }


        if (pivotDetected)
        {
            // Access data from 'Length' bars ago for the OB creation
            DateTime obTime = _identityHigh.Window[Length].EndTime; // Time of the pivot bar
            decimal obHigh = _identityHigh[Length];
            decimal obLow = _identityLow[Length];
            decimal obOpen = _identityOpen[Length];
            decimal obClose = _identityClose[Length];
            decimal obVolume = _identityVolume[Length];
            decimal obAtr = _atr[Length]; // ATR value 'Length' bars ago

            if (_direction == 1) // Bullish direction, look for bearish OB (formed by down move before up move)
            {
                // Pine: topP = obmode == "Length" ? (b.l[len] + 1 * atr[len]) > b.h[len] ? b.h[len] : (b.l[len] + 1 * atr[len]) : b.h[len]
                // Pine: blob.unshift(ob.new(topP, b.l[len], math.avg(topP, b.l[len]), b.t[len], obupcs, b.v[len], b.c[len] > b.o[len] ? 1 : -1, 1, 0, 0, b.t[len]))
                decimal topP = (ConstructionMode == ObConstructionMode.Length)
                                ? Math.Min(obHigh, obLow + 1 * obAtr)
                                : obHigh;
                decimal bottomP = obLow;
                int candleDir = obClose > obOpen ? 1 : -1;
                BullishBlocks.Insert(0, new OrderBlock(topP, bottomP, obTime, BullishColor, obVolume, candleDir));
            }
            else if (_direction == -1) // Bearish direction, look for bullish OB (formed by up move before down move)
            {
                // Pine: btmP = obmode == "Length" ? (b.h[len] - 1 * atr[len]) < b.l[len] ? b.l[len] : (b.h[len] - 1 * atr[len]) : b.l[len]
                // Pine: brob.unshift(ob.new(b.h[len], btmP, math.avg(btmP, b.h[len]), b.t[len], obdncs, b.v[len], b.c[len] > b.o[len] ? 1 : -1, 1, 0, 0, b.t[len]))
                 decimal bottomP = (ConstructionMode == ObConstructionMode.Length)
                                 ? Math.Max(obLow, obHigh - 1 * obAtr)
                                 : obLow;
                decimal topP = obHigh;
                int candleDir = obClose > obOpen ? 1 : -1;
                BearishBlocks.Insert(0, new OrderBlock(topP, bottomP, obTime, BearishColor, obVolume, candleDir));
            }
        }

        // --- Mitigate Order Blocks ---
        // Iterate backwards to allow safe removal
        // Bullish Blocks (Buy zones) - Mitigated if price goes below
        for (int i = BullishBlocks.Count - 1; i >= 0; i--)
        {
            var ob = BullishBlocks[i];
             if (ob.IsMitigated) continue; // Already marked

            for (int j = 0; j < Length; j++) // Check last 'len' bars including current
            {
                if (!CurrentBarHasEnoughHistory(j)) break;

                bool mitigated = false;
                decimal checkLow = _identityLow[j];
                decimal checkClose = _identityClose[j];
                decimal checkOpen = _identityOpen[j];

                switch (MitigationMethod)
                {
                    case ObMitigationMethod.Close:
                        mitigated = Math.Min(checkClose, checkOpen) < ob.Bottom;
                        break;
                    case ObMitigationMethod.Wick:
                        mitigated = checkLow < ob.Bottom;
                        break;
                    case ObMitigationMethod.Avg:
                        mitigated = checkLow < ob.Average;
                        break;
                }
                if (mitigated)
                {
                    ob.IsMitigated = true;
                    break; // Stop checking this OB
                }
            }
        }

        // Bearish Blocks (Sell zones) - Mitigated if price goes above
        for (int i = BearishBlocks.Count - 1; i >= 0; i--)
        {
             var ob = BearishBlocks[i];
             if (ob.IsMitigated) continue;

             for (int j = 0; j < Length; j++)
             {
                if (!CurrentBarHasEnoughHistory(j)) break;

                bool mitigated = false;
                decimal checkHigh = _identityHigh[j];
                decimal checkClose = _identityClose[j];
                decimal checkOpen = _identityOpen[j];

                switch (MitigationMethod)
                {
                    case ObMitigationMethod.Close:
                        mitigated = Math.Max(checkClose, checkOpen) > ob.Top;
                        break;
                    case ObMitigationMethod.Wick:
                        mitigated = checkHigh > ob.Top;
                        break;
                    case ObMitigationMethod.Avg:
                         mitigated = checkHigh > ob.Average;
                        break;
                }
                 if (mitigated)
                {
                    ob.IsMitigated = true;
                    break;
                }
            }
        }

        // Remove mitigated blocks
        BullishBlocks.RemoveAll(ob => ob.IsMitigated);
        BearishBlocks.RemoveAll(ob => ob.IsMitigated);


         // --- Update Metrics ('umt' logic) ---
         // This updates internal counters, might be used for visual display later
        if (_barTimeSpan > TimeSpan.Zero) // Only update if we have a valid time span
        {
            foreach (var ob in BullishBlocks) { ob.UpdateMetrics(_barTimeSpan); }
            foreach (var ob in BearishBlocks) { ob.UpdateMetrics(_barTimeSpan); }
        }

        // --- Handle Overlap ---
        if (HideOverlap)
        {
            RemoveOverlappingBlocks(BullishBlocks);
            RemoveOverlappingBlocks(BearishBlocks);
        }

        // --- Calculate Signal (Example) ---
        Signal = 0;
        if (BullishBlocks.Any())
        {
            var firstBullish = BullishBlocks[0];
            // Check if low entered the most recent bullish block
             if (bar.Low < firstBullish.Top && _identityLow[1] >= firstBullish.Top)
             {
                 Signal = 1; // Entered Bullish OB
             }
        }
         if (BearishBlocks.Any())
        {
            var firstBearish = BearishBlocks[0];
             // Check if high entered the most recent bearish block
             if (bar.High > firstBearish.Bottom && _identityHigh[1] <= firstBearish.Bottom)
             {
                 Signal = -1; // Entered Bearish OB
             }
        }


        // --- Return Value ---
        // An indicator must return a decimal. We can return the Signal,
        // or 0, or maybe the average price of the most recent block.
        // Returning the Signal makes it easy for an algorithm to react.
        return Signal;
    }

    // Helper to check if enough history exists for lookbacks
    private bool CurrentBarHasEnoughHistory(int lookback)
    {
         // Check against the underlying Identity indicators which store history
         return _identityHigh.IsReady && _identityHigh.Window.Count > lookback;
         //return Samples > Math.Max(lookback, WarmUpPeriod); // Alternative check
    }


    // Helper method to remove overlapping blocks (Pine: overlap method)
    private void RemoveOverlappingBlocks(List<OrderBlock> blocks)
    {
        if (blocks.Count <= 1) return;

        // Iterate backwards to safely remove items
        for (int i = blocks.Count - 1; i >= 1; i--)
        {
             // In Pine, 'current' was index 0 after unshift. Here, index 0 is the newest.
             // Pine compared id.get(i) with id.get(0). So compare older blocks [i] with newest [0].
             var olderBlock = blocks[i];
             var newestBlock = blocks[0]; // The most recently added block

             // Check for overlap conditions based on Pine script logic:
             bool overlaps =
                // Older bottom is within newest block
                (olderBlock.Bottom >= newestBlock.Bottom && olderBlock.Bottom <= newestBlock.Top) ||
                // Older top is within newest block
                (olderBlock.Top <= newestBlock.Top && olderBlock.Top >= newestBlock.Bottom) ||
                 // Older block completely engulfs newest block
                (olderBlock.Top >= newestBlock.Top && olderBlock.Bottom <= newestBlock.Bottom) ||
                // Newest block completely engulfs older block (Redundant if comparing i vs 0, but kept for clarity)
                (newestBlock.Top >= olderBlock.Top && newestBlock.Bottom <= olderBlock.Bottom);


             // Pine logic seems slightly different: compares stuff(i) vs current(0)
             // Let's re-read: stuff.btm > current.btm and stuff.btm < current.top => remove(i)
             // stuff.top < current.top and stuff.btm > current.btm => remove(i) (Completely inside)
             // stuff.top > current.top and stuff.btm < current.btm => remove(i) (Engulfs)
             // stuff.top < current.top and stuff.top > current.btm => remove(i)

             bool pineOverlap1 = olderBlock.Bottom > newestBlock.Bottom && olderBlock.Bottom < newestBlock.Top;
             bool pineOverlap2 = olderBlock.Top < newestBlock.Top && olderBlock.Bottom > newestBlock.Bottom; // Inside
             bool pineOverlap3 = olderBlock.Top > newestBlock.Top && olderBlock.Bottom < newestBlock.Bottom; // Engulfs
             bool pineOverlap4 = olderBlock.Top < newestBlock.Top && olderBlock.Top > newestBlock.Bottom;

            if (pineOverlap1 || pineOverlap2 || pineOverlap3 || pineOverlap4)
            {
                blocks.RemoveAt(i);
            }
        }
    }


    // Override Reset to clear lists and state
    public override void Reset()
    {
        _direction = 0;
        BullishBlocks.Clear();
        BearishBlocks.Clear();
        _high.Reset();
        _low.Reset();
        _atr.Reset();
        _pivotHigh.Reset();
        _identityHigh.Reset();
        _identityLow.Reset();
        _identityOpen.Reset();
        _identityClose.Reset();
        _identityVolume.Reset();
         _previousBarTime = DateTime.MinValue;
        _barTimeSpan = TimeSpan.Zero;
        Signal = 0;
        base.Reset();
    }
    public override bool IsReady { get; }

    public int WarmUpPeriod { get; }
}


public class OrderBlock
{
    public decimal Top { get; set; }
    public decimal Bottom { get; set; }
    public decimal Average { get; set; }
    public DateTime LocationTime { get; set; } // Use DateTime for time
    public Color BlockColor { get; set; }
    public decimal Volume { get; set; }
    public int Direction { get; set; } // 1 for bullish origin, -1 for bearish origin
    public int MoveCounter { get; set; } // Tracks state for umt logic
    public int BullishTouches { get; set; } // Equivalent to blPOS
    public int BearishTouches { get; set; } // Equivalent to brPOS
    public DateTime BullishTouchTime { get; set; } // Estimated time for bullish touches
    public DateTime BearishTouchTime { get; set; } // Estimated time for bearish touches
    public bool IsMitigated { get; set; } = false; // Flag to mark for removal

    public OrderBlock(decimal top, decimal bottom, DateTime time, Color color, decimal volume, int direction)
    {
        Top = top;
        Bottom = bottom;
        Average = (top + bottom) / 2;
        LocationTime = time;
        BlockColor = color;
        Volume = volume;
        Direction = direction; // Direction of the *candle* that formed the OB
        MoveCounter = 1; // Initial state
        BullishTouches = 0;
        BearishTouches = 0;
        BullishTouchTime = time; // Initialize times
        BearishTouchTime = time;
    }

    // Method equivalent to Pine Script's 'umt'
    public void UpdateMetrics(TimeSpan barTimeSpan)
    {
        // Original Pine uses bar index/time multiplication, which assumes fixed intervals.
        // Using bar count or estimated time additions is more robust in Lean.
        // This implementation increments counts and *estimates* future times.
        // A QCAlgorithm could potentially draw labels at these estimated times/counts later.

        if (Direction == 1) // Bullish candle created the block
        {
            switch (MoveCounter)
            {
                case 1: BullishTouches++; MoveCounter = 2; break;
                case 2: BullishTouches++; MoveCounter = 3; break;
                case 3: BearishTouches++; MoveCounter = 1; break;
            }
        }
        else // Bearish candle created the block (-1)
        {
            switch (MoveCounter)
            {
                case 1: BearishTouches++; MoveCounter = 2; break;
                case 2: BearishTouches++; MoveCounter = 3; break;
                case 3: BullishTouches++; MoveCounter = 1; break;
            }
        }

        // Estimate future touch times (simple approach, might need refinement)
        if (barTimeSpan > TimeSpan.Zero)
        {
             BullishTouchTime = LocationTime + TimeSpan.FromTicks(barTimeSpan.Ticks * BullishTouches);
             BearishTouchTime = LocationTime + TimeSpan.FromTicks(barTimeSpan.Ticks * BearishTouches);
        }
    }
}
