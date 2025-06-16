using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Indicators;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;


namespace QuantConnect.Algorithm.CSharp;

public class AAAOBDeepSeek : QCAlgorithm
{
    // Input parameters
    private readonly bool _showLast = true;
    private readonly int _oblast = 5;
    private readonly Color _obupcs = Color.FromArgb(90, 8, 153, 129);   // #089981 with 90% opacity
    private readonly Color _obdncs = Color.FromArgb(90, 242, 54, 69);   // #f23645 with 90% opacity
    private readonly bool _obshowactivity = true;
    private readonly Color _obactup = Color.FromArgb(50, 8, 153, 129);
    private readonly Color _obactdn = Color.FromArgb(50, 242, 54, 69);
    private readonly string _obmode = "Length";
    private readonly int _len = 5;
    private readonly string _obmiti = "Close";
    private readonly bool _showmetric = true;
    private readonly bool _showline = true;
    private readonly bool _overlap = true;

    // State variables
    private AverageTrueRange _atr;
    private RollingWindow<decimal> _atrWindow;
    private RollingWindow<TradeBar> _priceWindow;
    private int _dir = 0;
    private List<OrderBlockDeepSeek> _blob = new List<OrderBlockDeepSeek>();
    private List<OrderBlockDeepSeek> _brob = new List<OrderBlockDeepSeek>();

    public override void Initialize()
    {
        SetStartDate(2020, 1, 1);
        SetEndDate(2023, 1, 1);
        SetCash(100000);
        AddEquity("SPY", Resolution.Daily);

        _atr = new AverageTrueRange(_len);
        _atrWindow = new RollingWindow<decimal>(_len + 1);
        _priceWindow = new RollingWindow<TradeBar>(2 * _len + 1);

        // Warm-up indicators and windows
        WarmUpIndicator("SPY", _atr, Resolution.Daily);
    }

    public override void OnData(Slice slice)
    {
        if (!slice.Bars.ContainsKey("SPY")) return;

        var bar = slice.Bars["SPY"];
        _priceWindow.Add(bar);
        _atrWindow.Add(_atr.Current.Value);

        if (!_priceWindow.IsReady || !_atrWindow.IsReady) return;

        // Calculate highest high and lowest low for the last 'len' bars
        var lookback = _priceWindow.Skip(_priceWindow.Count - _len - 1).Take(_len).ToList();
        var up = lookback.Max(b => b.High);
        var dn = lookback.Min(b => b.Low);

        // Determine direction
        var currentHigh = _priceWindow[_priceWindow.Count - _len - 1].High;
        var currentLow = _priceWindow[_priceWindow.Count - _len - 1].Low;
        _dir = currentHigh > up ? -1 : currentLow < dn ? 1 : _dir;

        // Detect pivot high in volume
        bool isPivotHigh = true;
        int pivotIndex = _priceWindow.Count - _len - 1;
        for (int i = pivotIndex - _len; i <= pivotIndex + _len; i++)
        {
            if (i < 0 || i >= _priceWindow.Count) continue;
            if (_priceWindow[i].Volume > _priceWindow[pivotIndex].Volume)
            {
                isPivotHigh = false;
                break;
            }
        }

        // Create order blocks if pivot detected
        if (isPivotHigh)
        {
            var pivotBar = _priceWindow[pivotIndex];
            decimal atrValue = _atrWindow[_atrWindow.Count - _len - 1];

            decimal topP, btmP;
            if (_obmode == "Length")
            {
                btmP = (pivotBar.High - atrValue) < pivotBar.Low ? pivotBar.Low : pivotBar.High - atrValue;
                topP = (pivotBar.Low + atrValue) > pivotBar.High ? pivotBar.High : pivotBar.Low + atrValue;
            }
            else
            {
                topP = pivotBar.High;
                btmP = pivotBar.Low;
            }

            var newBlock = new OrderBlockDeepSeek
            {
                Top = topP,
                Bottom = btmP,
                Avg = (topP + btmP) / 2,
                Location = pivotBar.EndTime,
                Color = _dir == 1 ? _obupcs : _obdncs,
                Volume = pivotBar.Volume,
                Direction = _dir,
                Move = 1
            };

            if (_dir == 1) _blob.Insert(0, newBlock);
            else _brob.Insert(0, newBlock);
        }

        // Mitigation checks
        CheckMitigation(_blob, bar, true);
        CheckMitigation(_brob, bar, false);

        // Remove overlaps
        if (_overlap)
        {
            RemoveOverlaps(_blob);
            RemoveOverlaps(_brob);
        }

        // Draw order blocks
        DrawOrderBlocks();
    }

    private void CheckMitigation(List<OrderBlockDeepSeek> blocks, TradeBar currentBar, bool isBullish)
    {
        for (int i = blocks.Count - 1; i >= 0; i--)
        {
            var block = blocks[i];
            bool mitigated = false;

            for (int j = 0; j < _len; j++)
            {
                var pastBar = _priceWindow[_priceWindow.Count - j - 1];
                if (isBullish)
                {
                    if (_obmiti == "Close" && (Math.Min(pastBar.Close, pastBar.Open) < block.Bottom) ||
                        _obmiti == "Wick" && pastBar.Low < block.Bottom ||
                        _obmiti == "Avg" && pastBar.Low < block.Avg)
                    {
                        mitigated = true;
                        break;
                    }
                }
                else
                {
                    if (_obmiti == "Close" && (Math.Max(pastBar.Close, pastBar.Open) > block.Top) ||
                        _obmiti == "Wick" && pastBar.High > block.Top ||
                        _obmiti == "Avg" && pastBar.High > block.Avg)
                    {
                        mitigated = true;
                        break;
                    }
                }
            }

            if (mitigated) blocks.RemoveAt(i);
        }
    }

    private static void RemoveOverlaps(List<OrderBlockDeepSeek> blocks)
    {
        for (int i = blocks.Count - 1; i > 0; i--)
        {
            var current = blocks[i];
            var previous = blocks[i - 1];

            if ((current.Bottom > previous.Bottom && current.Bottom < previous.Top) ||
                (current.Top < previous.Top && current.Bottom > previous.Bottom) ||
                (current.Top > previous.Top && current.Bottom < previous.Bottom) ||
                (current.Top < previous.Top && current.Top > previous.Bottom))
            {
                blocks.RemoveAt(i);
            }
        }
    }

    private void DrawOrderBlocks()
    {
        // Remove previous drawings
        foreach (var chart in GetChartUpdates())
        {
            var chartName = chart.Name;
            foreach (var series in chart.Series.Values.Where(s => s is Rectangle))
            {
                chart.Series.Remove(series.Name);
            }
        }

        // Draw new blocks
        DrawBlocks(_blob, _obupcs);
        DrawBlocks(_brob, _obdncs);
    }

    private void DrawBlocks(List<OrderBlockDeepSeek> blocks, Color color)
    {
        var count = Math.Min(_oblast, blocks.Count);
        for (int i = 0; i < count; i++)
        {
            var block = blocks[i];
            // Plot("Order Blocks", new Rectangle
            // {
            //     Time1 = block.Location,
            //     Time2 = Time,
            //     Price1 = block.Top,
            //     Price2 = block.Bottom,
            //     Color = color
            // });
        }
    }
}

public class OrderBlockDeepSeek
{
    public decimal Top { get; set; }
    public decimal Bottom { get; set; }
    public decimal Avg { get; set; }
    public DateTime Location { get; set; }
    public Color Color { get; set; }
    public decimal Volume { get; set; }
    public int Direction { get; set; }
    public int Move { get; set; }
}
