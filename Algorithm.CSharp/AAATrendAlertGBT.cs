using System.Collections.Generic;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using System;
using System.Linq;

public class AAATrendAlertGBT : QCAlgorithm, IRegressionAlgorithmDefinition
{
    List<string> Symbols = new();
    private string symbolName = "XAUUSD";
    private Symbol symbol;
    Chart qcChart;

    private HeikinAshi _ltHA;
    private HeikinAshi _mtHA;

    private ExponentialMovingAverage _mtEma20;
    private decimal _prevMtEma20 = 0;

    private int _trendDirection = 0;
    private int rangeCount = 0;

    public override void Initialize()
    {
        SetStartDate(2025, 1, 1);
        SetEndDate(2025, 4, 4);
        SetCash(100000);

        Symbols.Add(AddData<AAAHour4>(symbolName).Symbol);
        Symbols.Add(AddData<AAADaily>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;


        _ltHA = new HeikinAshi("LT");
        _mtHA = new HeikinAshi("MT");
        _mtEma20 = EMA(symbol, 20);
        qcChart = new Chart(symbolName);
        
        AddChart(qcChart);
        Settings.DailyPreciseEndTime = false;

    }

    public override void OnData(Slice data)
    {
        
        
        if (data.First().Value is AAAHour4 value)
        {
            TradeBar currentBar = value.ToTradeBarWithoutSymbol();
            Plot(symbolName, Symbols[0], currentBar);
            Securities[symbol].Update(new List<BaseData> { value.ToTradeBar() }, currentBar.GetType());

            _mtHA.Update(currentBar);
            _mtEma20.Update(currentBar.Time, currentBar.Close);
          
        
            if (!_ltHA.IsReady || !_mtHA.IsReady || !_mtEma20.IsReady)
                return;

            // Step 1: Long Term Direction
            bool ltLong = _ltHA.Close > _ltHA.Open;
            bool ltShort = _ltHA.Close < _ltHA.Open;

            // Step 2: Mid Term Trend
            var mtEma20Delta = _mtEma20.Current.Value - _prevMtEma20;
            bool mtLong = _mtHA.Close > _mtHA.Open && _mtHA.Close > _mtEma20 && mtEma20Delta > 0;
            bool mtShort = _mtHA.Close < _mtHA.Open && _mtHA.Close < _mtEma20 && mtEma20Delta < 0;

            _prevMtEma20 = _mtEma20.Current.Value;

            // Combine steps
            bool isLong = ltLong && mtLong;
            bool isShort = ltShort && mtShort;

            _trendDirection = isLong ? 1 : isShort ? -1 : 0;

            Debug($"Time: {Time} | Trend: {_trendDirection}");


                Console.WriteLine("Current.Value:" + _trendDirection);
                if (_trendDirection == 1m)
                {
                    rangeCount = 0;
                    if (!Portfolio.Invested)
                    {
                        var orderTicket = MarketOrder(symbol, 1);
                        Log($"MarketOrder: {orderTicket}");
                    }

                    // if (Portfolio.Invested && rangeCount > 0)
                    // {
                    //     Liquidate(symbolName);
                    //     rangeCount = 0;
                    //     var orderTicket = MarketOrder(symbol, 1);
                    //     Log($"MarketOrder: {orderTicket}");
                    // }
                }
                else if (_trendDirection == -1m)
                {
                    rangeCount = 0;
                    if (!Portfolio.Invested)
                    {
                        var orderTicket = MarketOrder(symbol, -1);
                        Log($"MarketOrder: {orderTicket}");
                    }

                    // if (Portfolio.Invested && rangeCount > 0)
                    // {
                    //     Liquidate(symbolName);
                    //     rangeCount = 0;
                    //     var orderTicket = MarketOrder(symbol, -1);
                    //     Log($"MarketOrder: {orderTicket}");
                    // }
                }
                else if (_trendDirection == 0m)
                {
                    rangeCount++;
                    if (rangeCount == 5)
                    {
                        Liquidate(symbolName);
                    }
                }

        }else if (data.First().Value is AAADaily valuedaily)
        {
            TradeBar currentBar = valuedaily.ToTradeBarWithoutSymbol();
            Plot(symbolName, Symbols[1], currentBar);
            Securities[symbol].Update(new List<BaseData> { valuedaily.ToTradeBar() }, currentBar.GetType());
            _ltHA.Update(currentBar);
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
