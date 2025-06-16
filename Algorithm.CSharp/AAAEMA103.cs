namespace QuantConnect.Algorithm.CSharp;

using System.Collections.Generic;
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
using QuantConnect.Indicators;
using Microsoft.CodeAnalysis.Operations;
using System;

public class AAAEMA103 : QCAlgorithm, IRegressionAlgorithmDefinition
{
    private string symbolName = "EURUSD";
    private Symbol symbol;
    private DojiStar testIndicator;
    List<string> Symbols = new();

    private SimpleMovingAverage simpleMovingAverage;
    private PivotPointsHighLow pivotHighLow;
    private Chart qcChart;
    private bool is50PercentInvested = false;
    private bool is30PercentInvested = false;
    private bool is20PercentInvested = false;

    public override void Initialize()
    {
        SetStartDate(2025, 01, 01);
        SetEndDate(2025, 04, 04);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute15>(symbolName).Symbol);
        symbol = AddForex(symbolName).Symbol;
        SetWarmUp(15);

        qcChart = new Chart(symbolName);
        AddChart(qcChart);
        Settings.DailyPreciseEndTime = false;

        testIndicator = new DojiStar(symbolName);

        simpleMovingAverage = SMA(symbolName, 103);
        simpleMovingAverage.Window.Size = 105;
        pivotHighLow = PPHL(symbolName, 100, 100, 300);

        Schedule.On(DateRules.WeekEnd(), TimeRules.At(23, 50), OnMarketClose);
    }

    private void OnMarketClose()
    {

    }

    public override void OnData(Slice slice)
    {
        if (slice.First().Value is AAAMinute15 minute15)
        {
            TradeBar currentBar = minute15.ToTradeBarWithoutSymbol();
            Plot(symbolName, Symbols[0], currentBar);
            Securities[symbol].Update(new List<BaseData> { minute15.ToTradeBar() }, currentBar.GetType());
            pivotHighLow.Update(currentBar);
            simpleMovingAverage.Update(currentBar);


            if (IsWarmingUp) return;
            decimal sumOfRangeOfPreviousPrice = 0;
            int countSum = 5;
            for (int i = 2; i < countSum+2; i++)
            {
                decimal summer = simpleMovingAverage.Window[simpleMovingAverage.Window.Count - i];
                sumOfRangeOfPreviousPrice += summer;
            }
            sumOfRangeOfPreviousPrice /= countSum;

            if (simpleMovingAverage.IsReady)
            {

                if (Math.Round(sumOfRangeOfPreviousPrice, 5) < Math.Round(simpleMovingAverage.Current.Value, 5))
                {
                    Console.WriteLine(currentBar.Time + " : " + sumOfRangeOfPreviousPrice);
                    // if (!is50PercentInvested) {
                    is50PercentInvested = true;
                        var targetPercent = 0.5;
                        var orderQuantity = CalculateOrderQuantity(symbol, targetPercent);
                        MarketOrder(symbol, orderQuantity);
                    // }
                }
                else if ((simpleMovingAverage.Current.Value * -1.006m) == Securities[symbol].Price)
                {
                    // if (!is30PercentInvested)
                    // {
                        is30PercentInvested = true;
                        var targetPercent = 0.3;
                        var orderQuantity = CalculateOrderQuantity(symbol, targetPercent);
                        MarketOrder(symbol, orderQuantity);
                    // }
                }
                else if ((simpleMovingAverage.Current.Value * -1.007m) == Securities[symbol].Price)
                {
                    // if (!is30PercentInvested)
                    // {
                        is30PercentInvested = true;
                        var targetPercent = 0.2;
                        var orderQuantity = CalculateOrderQuantity(symbol, targetPercent);
                        MarketOrder(symbol, orderQuantity);
                    // }
                }
                if ((simpleMovingAverage.Current.Value * 1.006m) == Securities[symbol].Price)
                {
                    var holding = Portfolio[symbol];
                    if (holding.Quantity != 0)
                    {
                        var quantityToClose = holding.Quantity * 0.3m;
                        var quantity = (int)Math.Round(Math.Abs(quantityToClose), MidpointRounding.AwayFromZero);
                        if (quantity > 0)
                        {
                            if (holding.Quantity > 0)
                                MarketOrder(symbol, -quantity);
                            else
                                MarketOrder(symbol, quantity);
                        }
                    }
                }
                if ((simpleMovingAverage.Current.Value * 1.007m) == Securities[symbol].Price)
                {
                    var holding = Portfolio[symbol];
                    if (holding.Quantity != 0)
                    {
                        var quantityToClose = holding.Quantity * 0.2m;
                        var quantity = (int)Math.Round(Math.Abs(quantityToClose), MidpointRounding.AwayFromZero);
                        if (quantity > 0)
                        {
                            if (holding.Quantity > 0)
                                MarketOrder(symbol, -quantity);
                            else
                                MarketOrder(symbol, quantity);
                        }
                    }
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
        AAAChartLauncher.Launch(qcChart.Series, Symbols, Statistics, false);
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
