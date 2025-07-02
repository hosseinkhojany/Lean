using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Indicators.CandlestickPatterns;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp;

public class AAATrendAlert : QCAlgorithm, IRegressionAlgorithmDefinition
{
    List<string> Symbols = new();
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    TrendAlertIndicator trendAlertIndicator;
    private SimpleMovingAverage simpleMovingAverage;
    private PivotPointsHighLow pivotHighLow;
    private Engulfing engulfing;
    private int rangeCount = 0;
    private decimal currentTrend = 0;
    
    //TrendAlert Indicator components
    HeikinAshi ltHA;
    HeikinAshi mtHA;
    ExponentialMovingAverage mtEMA20;

    Dictionary<string, List<TradeBar>> series = new();
    private RollingWindow<TradeBar> rollingWindowsCandle15m = new RollingWindow<TradeBar>(200);
    private RollingWindow<TradeBar> rollingWindowsCandle2h = new RollingWindow<TradeBar>(200);

    public override void Initialize()
    {
        SetStartDate(2025, 1, 1);
        SetEndDate(2025, 4, 4);
        SetCash(1000);

        Symbols.Add(AddData<AAAHour2>(symbolName).Symbol);
        Symbols.Add(AddData<AAADaily>(symbolName).Symbol);
        Symbols.Add(AddData<AAAMinute15>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;

        for (int i = 0; i < Symbols.Count; i++)
        {
            series[Symbols[i]] = new List<TradeBar>();
        }
        SetWarmUp(15);
        Settings.DailyPreciseEndTime = false;
        ltHA = new HeikinAshi("LT_HA");
        mtHA = new HeikinAshi("MT_HA");
        mtEMA20 = new ExponentialMovingAverage("MT_EMA20", 20);
        trendAlertIndicator = new TrendAlertIndicator(symbolName, ltHA, mtHA, mtEMA20);
        simpleMovingAverage = new SimpleMovingAverage(symbolName, 103);
        pivotHighLow = new PivotPointsHighLow(100, 100);
        engulfing = new Engulfing(symbolName);


    }

    private bool TimeIs(int day, int hour, int minute)
    {
        return Time.Day == day && Time.Hour == hour && Time.Minute == minute;
    }



    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAHour2 value)
        {
            TradeBar currentBar = value.ToTradeBarWithoutSymbol();
            rollingWindowsCandle2h.Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { value.ToTradeBar() }, currentBar.GetType());
            series[Symbols[0]].Add(currentBar);
            mtHA.Update(currentBar);
            mtEMA20.Update(currentBar.Time, currentBar.Close);
            trendAlertIndicator.Update(currentBar);
            engulfing.Update(currentBar);

            if (IsWarmingUp) return;

            if (trendAlertIndicator.IsReady)
            {
                if (TimeIs(3, 8, 00)){
                    Console.WriteLine();
                }
                Console.WriteLine(currentBar.Time + " Current.Value:" + trendAlertIndicator.Current.Value);
                if (trendAlertIndicator.Current.Value == 1m)
                {
                    if (!Portfolio.Invested)
                    {
                        var orderTicket = MarketOrder(symbol, 1);
                        Log($"MarketOrder: {orderTicket}");
                    }

                    if (currentTrend == -1)
                    {
                        Liquidate(symbolName);
                        var orderTicket = MarketOrder(symbol, 1);
                        Log($"MarketOrder: {orderTicket}");
                    }

                    rangeCount = 0;
                    currentTrend = 1;
                }
                else if (trendAlertIndicator.Current.Value == -1m)
                {
                    if (!Portfolio.Invested)
                    {
                        var orderTicket = MarketOrder(symbol, -1);
                        Log($"MarketOrder: {orderTicket}");
                    }

                    if (currentTrend == 1)
                    {
                        Liquidate(symbolName);
                        var orderTicket = MarketOrder(symbol, -1);
                        Log($"MarketOrder: {orderTicket}");
                    }

                    rangeCount = 0;
                    currentTrend = -1;
                }
                else if (trendAlertIndicator.Current.Value == 0m)
                {
                    rangeCount++;
                    if (engulfing.IsReady && engulfing.Current.Value == 1)
                    {
                        
                        rangeCount = 0;
                        return;
                    }

                    if (rangeCount == 5)
                    {
                        Liquidate(symbolName);
                    }
                }

            }
        }
        else if (slice.First().Value is AAADaily valuedaily)
        {
            TradeBar currentBar = valuedaily.ToTradeBarWithoutSymbol();
            series[Symbols[1]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { valuedaily.ToTradeBar() }, currentBar.GetType());
            ltHA.Update(currentBar);
        }
        else if (slice.First().Value is AAAMinute15 aaaMinute15)
        {
            TradeBar currentBar = aaaMinute15.ToTradeBarWithoutSymbol();
            rollingWindowsCandle15m.Add(currentBar);
            series[Symbols[2]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { aaaMinute15.ToTradeBar() }, currentBar.GetType());
            simpleMovingAverage.Update(currentBar);
            pivotHighLow.Update(currentBar);
            Console.WriteLine("Current Bar:" + currentBar + " " + currentBar.Time);
            if (simpleMovingAverage.IsReady)
            {
                Console.WriteLine("Current SMA:" + Math.Round(simpleMovingAverage.Current.Value));
            }

            if (pivotHighLow.IsReady)
            {
                if (pivotHighLow.Current.Value != 0)
                {
                    Console.WriteLine("Current Pivot:" + Math.Round(pivotHighLow.Current.Value) + " "+currentBar.Time);
                }
            }
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
