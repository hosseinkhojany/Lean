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
using System;

public class AAARandomPosition : QCAlgorithm, IRegressionAlgorithmDefinition
{
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    List<Symbol> Symbols = new();

    private Chart qcChart;
    private Random _random = new Random();
    private int _openOrderId = -1;
    private decimal _stopLossPoints = 1m; // $2 stop loss
    private decimal _pipSize = 0.01m; // 1 pip = $0.01 for XAUUSD
    private decimal _stopLossDistance; // in price
    private bool _positionOpen = false;

    public override void Initialize()
    {
        SetStartDate(2025, 04, 01);
        SetEndDate(2025, 04, 04);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(15);

        qcChart = new Chart(symbolName);
        AddChart(qcChart);
        Settings.DailyPreciseEndTime = false;

        _stopLossDistance = _stopLossPoints; // $2 for XAUUSD, 200 points = $2

        Schedule.On(DateRules.WeekEnd(), TimeRules.At(23, 50), OnMarketClose);
    }

    private void OnMarketClose()
    {
        // No-op
    }

    public override void OnData(Slice slice)
    {

        if (slice.First().Value is AAAMinute daily)
        {
            TradeBar currentBar = daily.ToTradeBarWithoutSymbol();
            Plot(symbolName, Symbols[0], currentBar);
            Console.WriteLine("Current bar:"+currentBar);
            Securities[symbol].Update(new List<BaseData> { daily.ToTradeBar() }, currentBar.GetType());
            if (IsWarmingUp) return;

        // Only act if no open position
        if (!Portfolio.Invested)
        {
            int randomNumber = _random.Next(1, 100000000); // 1 to 10 inclusive
            bool isOdd = randomNumber % 2 == 1;
            decimal quantity = 1; // You can randomize this if needed

            // Get current price
            var price = Securities[symbol].Price;
            if (price == 0) return;

            decimal stopLossPrice = 0;
            OrderDirection direction;
            if (isOdd)
            {
                // Sell
                direction = OrderDirection.Sell;
                stopLossPrice = price + _stopLossDistance;
                _openOrderId = MarketOrder(symbol, -quantity, false, $"Random SELL {randomNumber}").OrderId;
                // Set take profit $6 below entry for sell
                var takeProfitPrice = price - 3m;
                    StopLimitOrder(symbol, -quantity, stopLossPrice, takeProfitPrice);
            }
            else
            {
                // Buy
                direction = OrderDirection.Buy;
                stopLossPrice = price - _stopLossDistance;
                _openOrderId = MarketOrder(symbol, quantity, false, $"Random BUY {randomNumber}").OrderId;
                // Set take profit $6 above entry for buy
                var takeProfitPrice = price + 3m;
                    StopLimitOrder(symbol, -quantity, stopLossPrice, takeProfitPrice);
                }

            // Attach stop loss

            _positionOpen = true;
        }

        }
    }

    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        Log($"Order: {orderEvent}");
    }

    public override void OnEndOfAlgorithm()
    {
        AAAChartLauncher.Launch(qcChart, Symbols, null, Statistics, false);
    }

    public bool CanRunLocally { get; } = true;
    public List<Language> Languages { get; } = [Language.CSharp];

    public virtual long DataPoints => 0;
    public virtual int AlgorithmHistoryDataPoints => 0;
    public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

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
