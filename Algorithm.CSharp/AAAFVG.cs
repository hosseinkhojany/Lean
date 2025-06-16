using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp;

public class AAAFVG : QCAlgorithm, IRegressionAlgorithmDefinition
{
   
    private int _lookbackPeriod = 3;
    private decimal _minimumGapSize = 0.001m;

    private RollingWindow<TradeBar> _tradeBars;
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    List<string> Symbols = new();
    Dictionary<string, List<TradeBar>> series = new();

    //3365.78,3365.78,3364.55,3365.32,135, 6/11/2025 11:45:00 PM
    //"Open":3365.78,"High":3365.78,"Low":3364.55,"Close":3365.32,"EndTime":"2025-06-12T03:46:00Z"
    public override void Initialize()
    {
        SetStartDate(2025, 06, 12);
        SetEndDate(2025, 06, 13);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(15);

        Settings.DailyPreciseEndTime = false;
        _tradeBars = new RollingWindow<TradeBar>(_lookbackPeriod);
        for (int i = 0; i < Symbols.Count; i++)
        {
            series[Symbols[i]] = new List<TradeBar>();
        }

        Schedule.On(DateRules.WeekEnd(), TimeRules.At(23, 50), OnMarketClose);
    }

    private void OnMarketClose()
    {

    }

    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAMinute minute)
        {
            TradeBar customData = minute.ToTradeBarWithoutSymbol();
            Securities[symbol].Update(new List<BaseData> { minute.ToTradeBar() }, customData.GetType());
            _tradeBars.Add(customData);
            series[Symbols[0]].Add(customData);
            Console.WriteLine($"Time: {customData.EndTime}, Open: {customData.Open}, High: {customData.High}, Low: {customData.Low}, Close: {customData.Close}, Volume: {customData.Volume}");
            if (IsWarmingUp || !_tradeBars.IsReady) return;
            FVG();
        }
    }
    
    private void FVG()
    {
        var barFirst = _tradeBars[2];
        var barFVG = _tradeBars[1];
        var barCurrent = _tradeBars[0];

        bool isAllGreen = barCurrent.Close > barCurrent.Open && barFVG.Close > barFVG.Open && barFirst.Close > barFirst.Open;
        bool isAllRed = barCurrent.Close < barCurrent.Open && barFVG.Close < barFVG.Open && barFirst.Close < barFirst.Open;

        decimal gap = 0;

        if (isAllGreen)
        {
            gap = barCurrent.Low - barFirst.High;
        }
        else 
        {
            gap = barFirst.Low - barCurrent.High;
        }

        decimal fvgMidPrice = Math.Abs(barFVG.Close + barFVG.Open) / 2;
        
        decimal bodyCurrentBar = Math.Abs(barCurrent.Close - barCurrent.Open);
        decimal bodyFirstBar = Math.Abs(barFirst.Close - barFirst.Open);

        if (gap > bodyCurrentBar && (isAllGreen || isAllRed))
        {
            // decimal percentBar2OverBar1 = (bar2.Close - bar2.Open) / body1 * 100;
            // decimal percentBar2OverBar3 = (bar2.Close - bar2.Open) / body3 * 100;
            // string fvgType = DetermineFVGType(bar1, bar2, bar3);
            // Log($"FVG Valid: FVGType: {fvgType} Gap: {gap}, Percent Bar2 over Bar1: {percentBar2OverBar1}%, Percent Bar2 over Bar3: {percentBar2OverBar3}%");
            Log((isAllGreen ? "Green " : "Red ")+"FVG Detect at " + barFVG.Time);

            // Calculate the difference between entry price and stop loss  
            decimal stopLossToEntry = Math.Abs(fvgMidPrice - barCurrent.Low);

            // Calculate TP1 and TP2 based on the direction  
            Liquidate(symbol);
            decimal tp1 = isAllGreen ? Securities[symbol].Close + stopLossToEntry : Securities[symbol].Close - stopLossToEntry;
            decimal tp2 = isAllGreen ? Securities[symbol].Close + (stopLossToEntry * 2) : Securities[symbol].Close - (stopLossToEntry * 2);
            OrderTicket marketOrder = MarketOrder(symbol, isAllGreen ? 1 : -1, false, $"FVG Order at {barFVG.Time}");
            Console.WriteLine(marketOrder.ToString());
        }
    }
    
    
    public override void OnSecuritiesChanged(SecurityChanges changes)
    {

    }

    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        Log($"Order: {orderEvent}");
    }

    public override void OnEndOfAlgorithm()
    {
        AAAChartLauncher.Launch(series, Symbols, Statistics, false);
    }

    public bool CanRunLocally { get; } = true;
    public List<Language> Languages { get; } = [Language.CSharp];

    /// <summary>
    /// Data Points count of all timeslices of algorithm
    /// </summary>
    public virtual long DataPoints => 0;

    /// <summary>
    /// Data Points count of the algorithm history
    /// </summary>
    public virtual int AlgorithmHistoryDataPoints => 0;

    /// <summary>
    /// Final status of the algorithm
    /// </summary>
    public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

    /// <summary>
    /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
    /// </summary>
    public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
    {
        { "Total Orders", "1" },
        { "Average Win", "0%" },
        { "Average Loss", "0%" },
        { "Compounding Annual Return", "30.084%" },
        { "Drawdown", "5.400%" },
        { "Expectancy", "0" },
        { "Start Equity", "100000" },
        { "End Equity", "104393.19" },
        { "Net Profit", "4.393%" },
        { "Sharpe Ratio", "1.543" },
        { "Sortino Ratio", "2.111" },
        { "Probabilistic Sharpe Ratio", "58.028%" },
        { "Loss Rate", "0%" },
        { "Win Rate", "0%" },
        { "Profit-Loss Ratio", "0" },
        { "Alpha", "0.166" },
        { "Beta", "0.717" },
        { "Annual Standard Deviation", "0.136" },
        { "Annual Variance", "0.019" },
        { "Information Ratio", "1.254" },
        { "Tracking Error", "0.118" },
        { "Treynor Ratio", "0.293" },
        { "Total Fees", "$2.06" },
        { "Estimated Strategy Capacity", "$160000000.00" },
        { "Lowest Capacity Asset", "AAPL R735QTJ8XC9X" },
        { "Portfolio Turnover", "0.83%" },
        { "OrderListHash", "d38318f2dd0a38f11ef4e4fd704706a7" }
    };
}
