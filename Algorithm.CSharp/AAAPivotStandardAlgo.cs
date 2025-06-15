namespace QuantConnect.Algorithm.CSharp;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators.CandlestickPatterns;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

public class AAAPivotStandardAlgo : QCAlgorithm, IRegressionAlgorithmDefinition
{
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    private AAAPivotPointStandardIndicator testIndicator;
    List<string> Symbols = new();

    private Chart qcChart;

    public override void Initialize()
    {
        SetStartDate(2025, 01, 01);
        SetEndDate(2025, 04, 04);
        SetCash(10000);

        Symbols.Add(AddData<AAAHour4>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(15);

        qcChart = new Chart(symbolName);
        AddChart(qcChart);
        Settings.DailyPreciseEndTime = false;

        testIndicator = new AAAPivotPointStandardIndicator(symbolName);

        Schedule.On(DateRules.WeekEnd(), TimeRules.At(23, 50), OnMarketClose);
    }

    private void OnMarketClose()
    {

    }

    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAHour4 daily)
        {
            TradeBar currentBar = daily.ToTradeBarWithoutSymbol();
            Plot(symbolName, Symbols[0], currentBar);
            Securities[symbol].Update(new List<BaseData> { daily.ToTradeBar() }, currentBar.GetType());
            testIndicator.Update(currentBar);
            if (IsWarmingUp) return;

            if (testIndicator.IsReady)
            {
                DateTime targetDate = DateTime.Parse("2025-02-28 19:00:00", DateTimeFormatInfo.CurrentInfo, DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces);
                if (
                    currentBar.Time.Year == targetDate.Year &&
                    currentBar.Time.Month == targetDate.Month &&
                    currentBar.Time.Day == targetDate.Day &&
                    currentBar.Time.Hour == targetDate.Hour 
                )
                {
                    MarketOrder(symbolName, 1);
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
        AAAChartLauncher.Launch(qcChart.Series, Symbols,  Statistics, false);
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
