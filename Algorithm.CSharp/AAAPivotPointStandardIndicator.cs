using QuantConnect.Indicators;
using QuantConnect.Data.Market;
using System;

public class AAAPivotPointStandardIndicator : IndicatorBase<TradeBar>
{
    public IndicatorBase<IndicatorDataPoint> P { get; private set; }
    public IndicatorBase<IndicatorDataPoint> R1 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> R2 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> R3 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> R4 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> R5 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> S1 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> S2 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> S3 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> S4 { get; private set; }
    public IndicatorBase<IndicatorDataPoint> S5 { get; private set; }

    private TradeBar _previousBar;

    public decimal Value => P.Current.Value;

    public AAAPivotPointStandardIndicator(string name)
        : base(name)
    {
        P = new Identity(name + "_P");
        R1 = new Identity(name + "_R1");
        R2 = new Identity(name + "_R2");
        R3 = new Identity(name + "_R3");
        R4 = new Identity(name + "_R4");
        R5 = new Identity(name + "_R5");
        S1 = new Identity(name + "_S1");
        S2 = new Identity(name + "_S2");
        S3 = new Identity(name + "_S3");
        S4 = new Identity(name + "_S4");
        S5 = new Identity(name + "_S5");
    }

    public override bool IsReady => _previousBar != null;

    protected override decimal ComputeNextValue(TradeBar input)
    {
        if (_previousBar != null)
        {
            var high = _previousBar.High;
            var low = _previousBar.Low;
            var close = _previousBar.Close;

            var pivot = (high + low + close) / 3;
            var r1 = 2 * pivot - low;
            var s1 = 2 * pivot - high;
            var r2 = pivot + (high - low);
            var s2 = pivot - (high - low);
            var r3 = high + 2 * (pivot - low);
            var s3 = low - 2 * (high - pivot);
            var r4 = r3 + (high - low);
            var s4 = s3 - (high - low);
            var r5 = r4 + (high - low);
            var s5 = s4 - (high - low);

            var time = input.EndTime;

            P.Update(time, pivot);
            R1.Update(time, r1);
            R2.Update(time, r2);
            R3.Update(time, r3);
            R4.Update(time, r4);
            R5.Update(time, r5);
            S1.Update(time, s1);
            S2.Update(time, s2);
            S3.Update(time, s3);
            S4.Update(time, s4);
            S5.Update(time, s5);
        }

        _previousBar = input;
        return P.Current.Value;
    }
}
