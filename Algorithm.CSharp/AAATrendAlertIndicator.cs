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

    public decimal computeDeepSeek()
    {
        if (_emaMT.IsReady)
        {
            _emaWindow.Add(_emaMT.Current.Value);
        }
    
        if (!IsReady) return 0m;

        bool LTlong = _heikinAshiLT.Close > _heikinAshiLT.Open;
        bool LTshort = _heikinAshiLT.Close < _heikinAshiLT.Open;

        // Step 2: Mid-Term Trend (Heikin-Ashi MT + EMA)
        decimal currentEma = _emaWindow[0];   // Most recent EMA value
        decimal previousEma = _emaWindow[1];  // Previous EMA value
        decimal emaSlope = currentEma - previousEma;

        bool MTlong = _heikinAshiMT.Close > _heikinAshiMT.Open &&   // HA MT is green
            _heikinAshiMT.Close > currentEma &&           // Price above EMA
            emaSlope > 0;                                 // EMA sloping up

        bool MTshort = _heikinAshiMT.Close < _heikinAshiMT.Open &&  // HA MT is red
            _heikinAshiMT.Close < currentEma &&          // Price below EMA
            emaSlope < 0;                                // EMA sloping down

        // Combine conditions for final trend signal
        if (MTlong && LTlong) return 1m;     // Strong long signal
        if (MTshort && LTshort) return -1m;  // Strong short signal
        return 0m;                           // No clear trend
    }

    public decimal computeGemini()
    {
        decimal mtEMA20Delta = _emaMT.Current.Value - _previousMtEmaValue;
        _previousMtEmaValue = _emaMT.Current.Value;

        bool ltLong = _heikinAshiLT.Close > _heikinAshiLT.Open;
        bool ltShort = _heikinAshiLT.Close < _heikinAshiLT.Open;

        bool mtLong = _heikinAshiMT.Close > _heikinAshiMT.Open && _heikinAshiMT.Close > _emaMT && mtEMA20Delta > 0;
        bool mtShort = _heikinAshiMT.Close < _heikinAshiMT.Open && _heikinAshiMT.Close < _emaMT && mtEMA20Delta < 0;

        bool longCondition = mtLong && ltLong;
        bool shortCondition = mtShort && ltShort;

        return longCondition ? 1m : (shortCondition ? -1m : 0m);
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
            input.Close > currentEma &&
            slope > 0;

        bool MTshort = _heikinAshiMT.Close < _heikinAshiMT.Open &&
            input.Close < currentEma &&
            slope < 0;

        if (MTlong && LTlong)
            return 1m;
        else if (MTshort && LTshort)
            return -1m;
        else
            return 0m;
    }

    
    public decimal computePerplexity(TradeBar input)
    {
        if (_emaMT.IsReady)
        {
            _emaWindow.Add(_emaMT.Current.Value);
        }

        if (!IsReady)
            return 0m;

        // Step 1: LT direction
        bool LTlong = _heikinAshiLT.Close > _heikinAshiLT.Open;
        bool LTshort = _heikinAshiLT.Close < _heikinAshiLT.Open;

        // Step 2: MT trend
        bool MTlong = _heikinAshiMT.Close > _heikinAshiMT.Open
            && input.Close > _emaMT.Current.Value
            && (_emaWindow[0] - _emaWindow[1]) > 0;

        bool MTshort = _heikinAshiMT.Close < _heikinAshiMT.Open
            && input.Close < _emaMT.Current.Value
            && (_emaWindow[0] - _emaWindow[1]) < 0;

        // Combine conditions
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


