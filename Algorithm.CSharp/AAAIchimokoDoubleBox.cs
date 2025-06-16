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
using System;

public class AAAIchimokoDoubleBox : QCAlgorithm, IRegressionAlgorithmDefinition
{
    private string symbolName = "XAUUSD";
    private Symbol symbol;

    private IchimokuKinkoHyo _ichimoku;
    List<string> Symbols = new();

    Dictionary<string, List<TradeBar>> series = new();
    private RollingWindow<TradeBar> rollingWindows = new RollingWindow<TradeBar>(100);


    decimal previousLead1 = 0;
    decimal previousLead2 = 0;

    decimal box = 0;
    decimal stoploss = 0;

    TradeBar crossedCandle;
    bool breakout = false;
    bool falseBreakout = false;
    bool pullback = false;
    int puulbackValidRange = 12;

    OrderDirection orderDirection;


    public override void Initialize()
    {
        SetStartDate(2025, 01, 01);
        SetEndDate(2025, 04, 04);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute5>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(25);
        Settings.DailyPreciseEndTime = false;

        _ichimoku = new IchimokuKinkoHyo();
        _ichimoku.Window.Size = 3;

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
        if (slice.First().Value is AAAMinute5 daily)
        {
            TradeBar currentBar = daily.ToTradeBarWithoutSymbol();

            series[Symbols[0]].Add(currentBar);
            rollingWindows.Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { daily.ToTradeBar() }, currentBar.GetType());

            _ichimoku.Update(currentBar);

            if (IsWarmingUp) return;

            if (_ichimoku.IsReady)
            {
                //Console.WriteLine($"Time: {currentBar.Time}, Lead 1: {_ichimoku.SenkouA}, Lead 2: {_ichimoku.SenkouB}, Tenkan: {_ichimoku.Tenkan}, Kijun: {_ichimoku.Kijun}, Chikou: {_ichimoku.Chikou}, TenkanMax: {_ichimoku.TenkanMaximum}, TenkanMin: {_ichimoku.TenkanMinimum}, KijunMax: {_ichimoku.KijunMaximum}, KijunMin: {_ichimoku.KijunMinimum}, SenkouBMax: {_ichimoku.SenkouBMaximum}, SenkouBMin: {_ichimoku.SenkouBMinimum}, DelayedTenkanSenkouA: {_ichimoku.DelayedTenkanSenkouA}, DelayedKijunSenkouA: {_ichimoku.DelayedKijunSenkouA}, DelayedMaxSenkouB: {_ichimoku.DelayedMaximumSenkouB}, DelayedMinSenkouB: {_ichimoku.DelayedMinimumSenkouB}");
                decimal lead1 = _ichimoku.SenkouA;
                decimal lead2 = _ichimoku.SenkouB;
                decimal laggingSpanB = _ichimoku.Chikou;
                decimal baseLine = _ichimoku.Kijun;
                decimal conversionLine = _ichimoku.Tenkan;

                if (lead1 > lead2 && previousLead1 <= previousLead2)
                {

                    if (rollingWindows.Count >= 24)
                    {
                        TradeBar past24Candle = rollingWindows.Last<TradeBar>();
                        box = (past24Candle.High - past24Candle.Low);
                        stoploss = past24Candle.Low - box;
                        crossedCandle = past24Candle;
                        Log("24 candle past: " + past24Candle.Time);
                    }

                    //RED TO GREEN (GREEN-BUY) BOX SET TO BOTTOM 
                    Log("Lead1 (GREEN) has crossed above Lead2." + currentBar.Time);
                    orderDirection = OrderDirection.Buy;

                }

                if (lead2 > lead1 && previousLead2 <= previousLead1)
                {
                    if (rollingWindows.Count >= 24)
                    {
                        TradeBar past24Candle = rollingWindows.Last<TradeBar>();
                        box = (past24Candle.High - past24Candle.Low);
                        stoploss = box + past24Candle.High;
                        crossedCandle = past24Candle;
                        Log("24 candle past: " + past24Candle.Time);
                    }
                    //GREEN TO RED (RED-SELL) BOX SET TO TOP
                    Log("Lead2 (RED) has crossed above Lead1." + currentBar.Time);
                    orderDirection = OrderDirection.Sell;

                }

                if (crossedCandle != null && currentBar.Time - crossedCandle.Time < TimeSpan.FromMinutes(5 * puulbackValidRange))
                {
                    if (orderDirection == OrderDirection.Buy)
                    {
                        if (currentBar.Low <= crossedCandle.High)
                        {
                            MarketOrder(symbol, 1);
                        }
                    }
                    else
                    {

                        if (currentBar.High <= crossedCandle.Low)
                        {
                            MarketOrder(symbol, -1);
                        }
                    }
                }
                else
                {

                }

                previousLead1 = lead1;
                previousLead2 = lead2;
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
