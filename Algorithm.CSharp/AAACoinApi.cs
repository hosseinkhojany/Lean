
using QuantConnect.Data;


namespace QuantConnect.Algorithm.CSharp;

public class AAACoinApi : QCAlgorithm
{
    private string symbolName = "XAUUSD";
    public override void Initialize()
    {
        Settings.DailyPreciseEndTime = false;
        AddForex(symbolName, Resolution.Tick);
    }

    public override void OnData(Slice slice)
    {
        Debug("OnData: "+slice);
    }
        

}
