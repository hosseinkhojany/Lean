
using QuantConnect.Statistics;

namespace QuantConnect.Algorithm.CSharp;

using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Indicators.CandlestickPatterns;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using System;
using QuantConnect.Algorithm.CSharp;

public class AAARandomPosition : QCAlgorithm, IRegressionAlgorithmDefinition
{
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    List<string> Symbols = new();

    private Chart qcChart;
    private Random _random = new Random();
    private OrderTicket _marketOrderTicket;
    private OrderTicket _takeProfitTicket;
    private OrderTicket _stopLossTicket;
    private decimal SL = 0;
    private decimal TP = 0;
    private TradeDirection _tradeDirection;

    public override void Initialize()
    {
        SetStartDate(2025, 03, 01);
        SetEndDate(2025, 04, 04);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(15);

        qcChart = new Chart(symbolName);
        AddChart(qcChart);
        Settings.DailyPreciseEndTime = false;

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
            Console.WriteLine("Current bar:" + currentBar);
            Securities[symbol].Update(new List<BaseData> { daily.ToTradeBar() }, currentBar.GetType());
            if (IsWarmingUp) return;

            if (_takeProfitTicket != null)
            {

                // Order order = Transactions.GetOrderById(_marketOrderTicket.OrderId);
                Order order2 = Transactions.GetOrderById(_takeProfitTicket.OrderId);
                Order order3 = Transactions.GetOrderById(_stopLossTicket.OrderId);
                Console.WriteLine();
                
            }
            if (!Portfolio.Invested)
            {
                int randomNumber = _random.Next(1, 100000000);
                bool isOdd = randomNumber % 2 == 1;
                decimal quantity = 1;

                var price = Securities[symbol].Price;
                if (price == 0) return;


                if (isOdd) // Sell
                {
                    SL = price + 5m;
                    TP = price - 5m;
                    _tradeDirection = TradeDirection.Short;
                    _marketOrderTicket = MarketOrder(symbol, -quantity, false, $"Order SELL {randomNumber}");
                    // _takeProfitTicket = LimitOrder(symbol, -quantity, takeProfitPrice, $"Random SELL TP {randomNumber}");
                    // _stopLossTicket = StopMarketOrder(symbol, -quantity, stopLossPrice, $"Random SELL SL {randomNumber}");
                }
                else // Buy
                {
                    SL = price - 5m;
                    TP = price + 5m;
                    _tradeDirection = TradeDirection.Long;
                    _marketOrderTicket = MarketOrder(symbol, quantity, false, $"Order BUY {randomNumber}");
                    // _takeProfitTicket = LimitOrder(symbol, quantity, takeProfitPrice, $"Random BUY TP {randomNumber}");
                    // _stopLossTicket = StopMarketOrder(symbol, quantity, stopLossPrice, $"Random BUY SL {randomNumber}");
                }
            }
            else
            {
                var price = Securities[symbol].Price;
                if (_tradeDirection == TradeDirection.Long)
                {
                    if(price >= TP || price <= SL)
                    {
                        Liquidate(symbol);
                    }
                }
                else
                {
                    if(price <= TP || price >= SL)
                    {
                        Liquidate(symbol);
                    }
                }
            }
        }
    }

    public override void OnOrderEvent(OrderEvent orderEvent)
    {
        Log($"Order: {orderEvent}");

        // Check if the order event is for the initial market order
        if (_marketOrderTicket != null && orderEvent.OrderId == _marketOrderTicket.OrderId && orderEvent.Status == OrderStatus.Filled)
        {
            // The initial market order has filled.
            // No action needed here for position state, as the TP/SL will manage it.
            return;
        }
       
        // if ((_takeProfitTicket != null && orderEvent.OrderId == _takeProfitTicket.OrderId && orderEvent.Status == OrderStatus.Filled) ||
        //     (_stopLossTicket != null && orderEvent.OrderId == _stopLossTicket.OrderId && orderEvent.Status == OrderStatus.Filled))
        // {
        //     // One of the exit orders (TP or SL) has filled, meaning the position is closed.
        //     // Cancel the other outstanding order to prevent unintended trades.
        //     //if (_takeProfitTicket != null && _takeProfitTicket.Status != OrderStatus.Filled && _takeProfitTicket.Status != OrderStatus.Canceled)
        //     //{
        //     //    _takeProfitTicket.Cancel("Other exit order filled.");
        //     //}
        //     //if (_stopLossTicket != null && _stopLossTicket.Status != OrderStatus.Filled && _stopLossTicket.Status != OrderStatus.Canceled)
        //     //{
        //     //    _stopLossTicket.Cancel("Other exit order filled.");
        //     //}
        //
        //     // Reset the order tickets to null after a position is closed
        //     _marketOrderTicket = null;
        //     _takeProfitTicket = null;
        //     _stopLossTicket = null;
        // }
    }

    public override void OnEndOfAlgorithm()
    {
        AAAChartLauncher.Launch(qcChart.Series, Symbols, Statistics, false);
    }

    public bool CanRunLocally { get; } = true;
    public List<Language> Languages { get; } = [Language.CSharp];

    public virtual long DataPoints => 0;
    public virtual int AlgorithmHistoryDataPoints => 0;
    public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

    public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
    {
        { "Total Orders", "1" }, // This should be updated based on your actual test
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
