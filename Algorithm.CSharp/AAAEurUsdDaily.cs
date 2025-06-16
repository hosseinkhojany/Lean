using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp;

public class AAAEurUsdDaily : QCAlgorithm, IRegressionAlgorithmDefinition
{
    List<string> Symbols = new();
    private string symbolName = "EURUSD";
    private Symbol symbol;
    Chart qcChart;
    public override void Initialize()
    {
        SetStartDate(2020, 1, 1);
        SetEndDate(2025, 4, 4);
        SetCash(100000);

        Symbols.Add(AddData<AAADaily>(symbolName).Symbol);
        symbol = AddForex(symbolName).Symbol;
        SetWarmUp(5);
            
        qcChart = new Chart(symbolName);
        AddChart(qcChart);
        Settings.DailyPreciseEndTime = false;
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


private TradeBar previousBar;
private bool hasOpenPosition = false;
private decimal entryPrice = 0m;

public override void OnData(Slice data)
{
    if (data.First().Value is AAADaily daily)
    {
        TradeBar currentBar = daily.ToTradeBarWithoutSymbol();
        Plot(symbolName, Symbols[0], currentBar);
        Securities[symbol].Update(new List<BaseData> { daily.ToTradeBar() }, currentBar.GetType());

        // Entry logic: Long Only
        if (previousBar != null && !hasOpenPosition)
        {
            // 1. Outside Bar (current high > prev high and current low < prev low)
            bool isOutsideBar = currentBar.High > previousBar.High && currentBar.Low < previousBar.Low;
            // 2. Close < Yesterday Low
            bool closeBelowPrevLow = currentBar.Close < previousBar.Low;

            if (isOutsideBar && closeBelowPrevLow)
            {
                // 3. Buy next Open (simulate by buying at current open)
                var quantity = CalculateOrderQuantity(symbol, 0.95); // 95% of portfolio
                MarketOrder(symbol, -quantity, false, "LongEntry");
                entryPrice = currentBar.Open;
                hasOpenPosition = true;
                Log($"Entered long at {entryPrice} on {currentBar.EndTime}");
            }
        }

        // Exit logic
        if (hasOpenPosition)
        {
            var newYorkTime = Time.ConvertTo(TimeZones.NewYork, TimeZone);
            if (newYorkTime.TimeOfDay >= new TimeSpan(16, 45, 0) && newYorkTime.TimeOfDay <= new TimeSpan(16, 55, 0))
            {
                var holding = Portfolio[symbol];
                if (holding.UnrealizedProfit > 0)
                {
                    Liquidate(symbol, "ProfitExit");
                    hasOpenPosition = false;
                    Log($"Exited long for profit at {currentBar.Close} on {currentBar.EndTime}");
                }
            }
            // 2. Stop loss 200 pips (0.0200 for EURUSD)
            else if (currentBar.Low <= entryPrice - 0.0200m)
            {
                Liquidate(symbol, "StopLoss");
                hasOpenPosition = false;
                Log($"Exited long for stop loss at {currentBar.Low} on {currentBar.EndTime}");
            }
        }

        previousBar = currentBar;
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
            AAAChartLauncher.Launch(qcChart.Series, Symbols,Statistics,false);
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
