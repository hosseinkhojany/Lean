using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Indicators.CandlestickPatterns;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp;

public class AAASuperTrend : QCAlgorithm, IRegressionAlgorithmDefinition
{
    List<string> Symbols = new();
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    Dictionary<string, List<TradeBar>> series = new();
    TrendAlertIndicator trendAlertIndicator;
    private SimpleMovingAverage simpleMovingAverage;
    private PivotPointsHighLow pivotHighLow;
    private Engulfing engulfing;
    HeikinAshi ltHA;
    HeikinAshi mtHA;
    ExponentialMovingAverage mtEMA20;
    SuperTrend superTrend;
    int currentTrend = 4;

    private int previousTrend = 0;
    public override void Initialize()
    {
        SetStartDate(2025, 1, 1);
        SetEndDate(2025, 4, 4);
        SetCash(100000);

        Symbols.Add(AddData<AAADaily>(symbolName).Symbol);
        //Symbols.Add(AddData<AAAHour2>(symbolName).Symbol);
        //Symbols.Add(AddData<AAAMinute15>(symbolName).Symbol);
        Symbols.Add(AddData<AAAMinute5>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(5);

        for (int i = 0; i < Symbols.Count; i++)
        {
            series[Symbols[i]] = new List<TradeBar>();
        }

        Settings.DailyPreciseEndTime = false;

        superTrend = new SuperTrend(25, 3.0m);
        ltHA = new HeikinAshi("LT_HA");
        mtHA = new HeikinAshi("MT_HA");
        mtEMA20 = new ExponentialMovingAverage("MT_EMA20", 20);
        trendAlertIndicator = new TrendAlertIndicator(symbolName, ltHA, mtHA, mtEMA20);
        simpleMovingAverage = new SimpleMovingAverage(symbolName, 103);
        pivotHighLow = new PivotPointsHighLow(100, 100);
        engulfing = new Engulfing(symbolName);
    }


    /*
    
    Trade Entries
    Daily Chart EUR/USD

    1. Outside Bar (including the highs / lows)
    2. Close < Yesterday Low
    3. Buy next Open

    Long Only

     */
    /*
    
    Trade Exits
    1.At 16:45 - 16:55 EST check open profit
    2.If profit > 0 then exit trade
    3.Stop loss 200 pips

     */



    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAMinute5 m5)
        {
            TradeBar currentBar = m5.ToTradeBarWithoutSymbol();
            series[Symbols[1]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { m5.ToTradeBar() }, currentBar.GetType());

            superTrend.Update(currentBar);

            if (IsWarmingUp) return;

            if (superTrend.IsReady)
            {
                CheckTrendChange(superTrend, currentBar);
                //Console.WriteLine("superTrend.Current.Value:" + superTrend.Current.Value);
                //Console.WriteLine("CurrentTrailingUpperBand:" + superTrend.CurrentTrailingUpperBand);
                //Console.WriteLine("CurrentTrailingLowerBand:" + superTrend.CurrentTrailingLowerBand);
                //Console.WriteLine("BasicUpperBand:" + superTrend.BasicUpperBand);
                //Console.WriteLine("BasicLowerBand:" + superTrend.BasicLowerBand);
            }




        }
        else if (slice.First().Value is AAADaily d) { 

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

    public int GetCurrentTrend(SuperTrend superTrend)
    {
        if (superTrend.Current.Value > superTrend.CurrentTrailingUpperBand)
        {
            return 1;
        }
        else if (superTrend.Current.Value < superTrend.CurrentTrailingLowerBand)
        {
            return -1; 
        }
        else
        {
            return 0;
        }
    }

    private void CheckTrendChange(SuperTrend superTrend, TradeBar currentBar)
    {
        int currentTrend = GetCurrentTrend(superTrend);

        if (currentTrend != previousTrend)
        {
            Log($"Trend changed for {symbol} at {currentBar.Time}: Previous Trend = {previousTrend}, Current Trend = {currentTrend}");
            previousTrend = currentTrend;
        }
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
            {"Total Orders", "1"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "30.084%"},
            {"Drawdown", "5.400%"},
            {"Expectancy", "0"},
            {"Start Equity", "100000"},
            {"End Equity", "104393.19"},
            {"Net Profit", "4.393%"},
            {"Sharpe Ratio", "1.543"},
            {"Sortino Ratio", "2.111"},
            {"Probabilistic Sharpe Ratio", "58.028%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0.166"},
            {"Beta", "0.717"},
            {"Annual Standard Deviation", "0.136"},
            {"Annual Variance", "0.019"},
            {"Information Ratio", "1.254"},
            {"Tracking Error", "0.118"},
            {"Treynor Ratio", "0.293"},
            {"Total Fees", "$2.06"},
            {"Estimated Strategy Capacity", "$160000000.00"},
            {"Lowest Capacity Asset", "AAPL R735QTJ8XC9X"},
            {"Portfolio Turnover", "0.83%"},
            {"OrderListHash", "d38318f2dd0a38f11ef4e4fd704706a7"}
        };
}
