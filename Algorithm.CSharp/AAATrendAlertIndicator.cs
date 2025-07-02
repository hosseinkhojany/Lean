namespace QuantConnect.Algorithm.CSharp;

using QuantConnect.Indicators;
using QuantConnect.Data.Market;


public class TrendAlertIndicator : TradeBarIndicator
{
    private readonly HeikinAshi _heikinAshiLT;
    private readonly HeikinAshi _heikinAshiMT;
    private readonly ExponentialMovingAverage _emaMT;
    private readonly RollingWindow<decimal> _emaWindow;
    private decimal _previousMtEmaValue;

    public TrendAlertIndicator(
        string name,
        HeikinAshi longTermHA,
        HeikinAshi midTermHA,
        ExponentialMovingAverage midTermEMA20
        )
        : base(name)
    {
        _heikinAshiLT = longTermHA;
        _heikinAshiMT = midTermHA;
        _emaMT = midTermEMA20;
        _emaWindow = new RollingWindow<decimal>(2);
        _previousMtEmaValue = 0m;
    }

    public override bool IsReady =>
        _heikinAshiLT.IsReady && _heikinAshiMT.IsReady && _emaMT.IsReady && _emaWindow.IsReady;

    protected override decimal ComputeNextValue(TradeBar input)
    {
        return computePerplexityv2(input);
    }

    public decimal computePerplexityv2(TradeBar input)
    {
        if (_emaMT.IsReady)
        {
            _emaWindow.Add(_emaMT.Current.Value);
        }

        if (!IsReady || _emaWindow.Count < 2)
            return 0m;

        decimal currentEma = _emaWindow[0];
        decimal previousEma = _emaWindow[1];
        decimal slope = currentEma - previousEma;

        bool LTlong = _heikinAshiLT.Close > _heikinAshiLT.Open;
        bool LTshort = _heikinAshiLT.Close < _heikinAshiLT.Open;

        bool MTlong = _heikinAshiMT.Close > _heikinAshiMT.Open &&
             _heikinAshiMT.Close > currentEma &&
            slope > 0;

        bool MTshort = _heikinAshiMT.Close < _heikinAshiMT.Open &&
             _heikinAshiMT.Close < currentEma &&
            slope < 0;

        if (MTlong && LTlong)
            return 1m;
        else if (MTshort && LTshort)
            return -1m;
        else
            return 0m;
    }

    

    public override void Reset()
    {
        _heikinAshiLT.Reset();
        _heikinAshiMT.Reset();
        _emaMT.Reset();
        _emaWindow.Reset();
        _previousMtEmaValue = 0m;
        base.Reset();
    }
}


