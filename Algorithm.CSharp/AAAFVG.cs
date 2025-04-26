using System;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp;

public class AAAFVG : QCAlgorithm
{
   
    private Symbol xauusdSymbol;
    private int _lookbackPeriod = 3;
    private decimal _minimumGapSize = 0.001m;

    private RollingWindow<TradeBar> _tradeBars;
        
    public override void Initialize()
    {
        SetStartDate(2025, 01, 01);
        SetEndDate(2025, 04, 04);
        SetCash(100000);

        xauusdSymbol = AddCfd("XAUUSD", Resolution.Minute).Symbol;
        AddData<AAAMinute5>(xauusdSymbol);

        _tradeBars = new RollingWindow<TradeBar>(_lookbackPeriod);
        SetWarmUp(_lookbackPeriod);
    }

    public override void OnData(Slice data)
    {
        if (data.Bars.Count > 0)
        {
            TradeBar customData = data.Bars.First().Value;
            _tradeBars.Add(customData);
            Plot("chartName", xauusdSymbol.Value, customData);
            if (IsWarmingUp || !_tradeBars.IsReady) return;

            DetectFVGs();
            Log($"Time: {customData.Time}, Open: {customData.Open}, High: {customData.High}, Low: {customData.Low}, Close: {customData.Close}, Volume: {customData.Volume}");

        }
    }
    
    private void DetectFVGs()
    {
        var bar1 = _tradeBars[2];
        var bar2 = _tradeBars[1];
        var bar3 = _tradeBars[0];

        decimal gap = bar1.High - bar3.Low;
        decimal body1 = Math.Abs(bar1.Close - bar1.Open);
        decimal body3 = Math.Abs(bar3.Close - bar3.Open);
        if (bar1.High < bar3.Low && gap > body1 && gap > body3)
        {
            decimal percentBar2OverBar1 = (bar2.Close - bar2.Open) / body1 * 100;
            decimal percentBar2OverBar3 = (bar2.Close - bar2.Open) / body3 * 100;
            string fvgType = DetermineFVGType(bar1, bar2, bar3);
            Log($"FVG Valid: FVGType: {fvgType} Gap: {gap}, Percent Bar2 over Bar1: {percentBar2OverBar1}%, Percent Bar2 over Bar3: {percentBar2OverBar3}%");
        }
    }

    private string DetermineFVGType(TradeBar bar1, TradeBar bar2, TradeBar bar3)
    {
        bool isBar1Green = bar1.Close > bar1.Open;
        bool isBar2Green = bar2.Close > bar2.Open;
        bool isBar3Green = bar3.Close > bar3.Open;

        if (isBar1Green && isBar2Green && isBar3Green)
            return "Three Green Candles";
        if (isBar1Green && isBar2Green && !isBar3Green)
            return "Two Green, One Red";
        if (isBar1Green && !isBar2Green && !isBar3Green)
            return "One Green, Two Red";
        if (!isBar1Green && !isBar2Green && !isBar3Green)
            return "Three Red Candles";
        if (!isBar1Green && !isBar2Green && isBar3Green)
            return "Two Red, One Green";
        if (!isBar1Green && isBar2Green && isBar3Green)
            return "One Red, Two Green";
        if (!isBar1Green && isBar2Green && !isBar3Green)
            return "Red, Green, Red";
        if (isBar1Green && !isBar2Green && isBar3Green)
            return "Green, Red, Green";

        return "Unknown Pattern";
    }
}
