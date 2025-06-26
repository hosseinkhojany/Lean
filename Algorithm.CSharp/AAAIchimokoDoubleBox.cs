using MT.Models;
using MT.SharedOC;
using MtApi5;

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
using MT;

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

    TradeBar crossedCandle;
    TradeBar past24CrossedCandle;
    TradeBar breakoutCandle;
    bool breakout;
    bool falseBreakout;
    bool pullback;
    int pullbackCounter = 0;
    TradeBar currentBar;

    OrderDirection orderDirection;

    MT4 client = new MT4(true, 8222);


    public override void Initialize()
    {
        SetStartDate(2025, 01, 1);
        SetEndDate(2025, 05, 17);
        SetCash(10000);

        Symbols.Add(AddData<AAAMinute5>(symbolName).Symbol);
        Symbols.Add(AddData<AAAMinute>(symbolName).Symbol);
        symbol = AddCfd(symbolName).Symbol;
        SetWarmUp(100);
        Settings.DailyPreciseEndTime = false;

        _ichimoku = new IchimokuKinkoHyo(9, 26, 26, 52, 1, 1);

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
        if (slice.First().Value is AAAMinute aaaMinute)
        {
            currentBar = aaaMinute.ToTradeBarWithoutSymbol();
            series[Symbols[1]].Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { aaaMinute.ToTradeBar() }, currentBar.GetType());

        }
        if (slice.First().Value is AAAMinute5 daily)
        {
            currentBar = daily.ToTradeBarWithoutSymbol();

            series[Symbols[0]].Add(currentBar);
            rollingWindows.Add(currentBar);
            Securities[symbol].Update(new List<BaseData> { daily.ToTradeBar() }, currentBar.GetType());

            _ichimoku.Update(currentBar);

            if (IsWarmingUp) return;

            if (_ichimoku.IsReady)
            {
                // Console.WriteLine($"Time: {currentBar.Time}, Lead 1: {_ichimoku.SenkouA}, Lead 2: {_ichimoku.SenkouB}, Tenkan: {_ichimoku.Tenkan}, Kijun: {_ichimoku.Kijun}, Chikou: {_ichimoku.Chikou}, TenkanMax: {_ichimoku.TenkanMaximum}, TenkanMin: {_ichimoku.TenkanMinimum}, KijunMax: {_ichimoku.KijunMaximum}, KijunMin: {_ichimoku.KijunMinimum}, SenkouBMax: {_ichimoku.SenkouBMaximum}, SenkouBMin: {_ichimoku.SenkouBMinimum}, DelayedTenkanSenkouA: {_ichimoku.DelayedTenkanSenkouA}, DelayedKijunSenkouA: {_ichimoku.DelayedKijunSenkouA}, DelayedMaxSenkouB: {_ichimoku.DelayedMaximumSenkouB}, DelayedMinSenkouB: {_ichimoku.DelayedMinimumSenkouB}");
                decimal lead1 = _ichimoku.SenkouA;
                decimal lead2 = _ichimoku.SenkouB;
                decimal laggingSpanB = _ichimoku.Chikou;
                decimal baseLine = _ichimoku.Kijun;
                decimal conversionLine = _ichimoku.Tenkan;

                if (lead1 > lead2 && previousLead1 <= previousLead2 && previousLead1 > 0 && previousLead2 > 0)
                {

                    if (rollingWindows.Count >= 99)
                    {
                        TradeBar past24Candle = rollingWindows[1];
                        crossedCandle = currentBar;
                        past24CrossedCandle = past24Candle;
                        Log("24 candle past: " + past24Candle.Time);
                        //RED TO GREEN (GREEN-BUY) BOX SET TO BOTTOM 
                        Log("\n\n\n Lead1 (GREEN) has crossed above Lead2. " + currentBar.Time + " \n\n\n");
                        orderDirection = OrderDirection.Buy;
                        pullbackCounter = 0;
                        breakoutCandle = null;
                    }

                }

                if (lead2 > lead1 && previousLead2 <= previousLead1 && previousLead1 > 0 && previousLead2 > 0)
                {
                    if (rollingWindows.Count >= 99)
                    {
                        TradeBar past24Candle = rollingWindows[1];
                        crossedCandle = currentBar;
                        past24CrossedCandle = past24Candle;
                        Log("24 candle past: " + past24Candle.Time);
                        //GREEN TO RED (RED-SELL) BOX SET TO TOP
                        Log(" \n\n\nLead2 (RED) has crossed above Lead1. " + currentBar.Time+ "\n\n\n");
                        orderDirection = OrderDirection.Sell;
                        pullbackCounter = 0;
                        breakoutCandle = null;
                    }

                }
                //crossed
                if (past24CrossedCandle != null)
                {
                    if (orderDirection == OrderDirection.Buy)
                    {
                        //breakout for green
                        if (currentBar.Open < currentBar.Close)
                        {
                            if (currentBar.Open >= past24CrossedCandle.High)
                            {
                                breakoutCandle = currentBar;
                            }
                        }
                        //breakout for sell
                        else
                        {
                            if (currentBar.Close >= past24CrossedCandle.High)
                            {
                                breakoutCandle = currentBar;
                            }
                        }
                        //pullback
                        if (currentBar.Low <= past24CrossedCandle.High)
                        {

                            //3353.39 - 3348.81 - 3348.81 sell
                            //3353.39 - 3348.81 + 3353.39 buy
                            //check breakout and 12 candle achived or not 
                            if (breakoutCandle != null && pullbackCounter <= 12)
                            {
                                box = past24CrossedCandle.High - past24CrossedCandle.Low;
                                openPositions(true);
                                decimal tp3 = Math.Abs(box + past24CrossedCandle.High);
                                decimal sl3 = Math.Abs(box - past24CrossedCandle.Low);
                                // client.SendOrderAsync(
                                //     new SendOrderRq(
                                //         OrderTypeRequest.CREATE,
                                //         new MqlTradeRequest
                                //         {
                                //             Symbol = SymbolMapper.leanSymbolToMt(symbolName, MtMarketType.ORBEX, MTType.MT4),
                                //             Volume = 0.01,
                                //             Price = (double)Securities[symbol].Price,
                                //             Sl = (double)stoploss,
                                //             Tp = (double)takeProfit,
                                //         }
                                //     )
                                // );
                                Console.WriteLine(
                                    "\n\n{Open BUY " + "\n"
                                    + "Crossed: " + crossedCandle.Time + "\n"
                                    + "past24CrossedCandle: " + past24CrossedCandle.Time + "\n"
                                    + "BreakOut: " + breakoutCandle.Time + "\n"
                                    + "PullBack:" + currentBar.Time + "\n"
                                    + "STOPLOSS: " + sl3 + "\n"
                                    + "TAKEPROFIT: " + tp3 + "\n"
                                    + "BOXSize: " + box + "}\n\n"
                                    );
                                crossedCandle = null;
                                past24CrossedCandle = null;
                                breakoutCandle = null;
                            }
                        }
                    }
                    else
                    {
                        //breakout for green
                        if (currentBar.Open < currentBar.Close)
                        {
                            if (currentBar.Close <= past24CrossedCandle.Low)
                            {
                                breakoutCandle = currentBar;
                            }
                        }
                        //breakout for sell
                        else
                        {
                            if (currentBar.Open <= past24CrossedCandle.Low)
                            {
                                breakoutCandle = currentBar;
                            }
                        }
                        if (currentBar.High >= past24CrossedCandle.Low)
                        {

                            if (breakoutCandle != null && pullbackCounter <= 12)
                            {
                                box = past24CrossedCandle.High - past24CrossedCandle.Low;
                                openPositions(false);
                                decimal tp3 = Math.Abs(box - past24CrossedCandle.Low);
                                decimal sl3 = Math.Abs(box + past24CrossedCandle.High);
                                // client.SendOrderAsync(
                                //     new SendOrderRq(
                                //         OrderTypeRequest.CREATE,
                                //         new MqlTradeRequest
                                //         {
                                //             Symbol = SymbolMapper.leanSymbolToMt(symbolName, MtMarketType.ORBEX, MTType.MT4),
                                //             Volume = 0.01,
                                //             Price = (double)Securities[symbol].Price,
                                //             Sl = (double)stoploss,
                                //             Tp = (double)takeProfit,
                                //         }
                                //     )
                                // );
                                Console.WriteLine(
                                    "\n\n{Open SELL " + "\n"
                                    + "Crossed: " + crossedCandle.Time + "\n"
                                    + "past24CrossedCandle: " + past24CrossedCandle.Time + "\n"
                                    + "BreakOut: " + breakoutCandle.Time + "\n"
                                    + "PullBack:" + currentBar.Time + "\n"
                                    + "STOPLOSS: " + sl3 + "\n"
                                    + "TAKEPROFIT: " + tp3 + "\n"
                                    + "BOXSize: " + box + "}\n\n"
                                    );
                                crossedCandle = null;
                                past24CrossedCandle = null;
                                breakoutCandle = null;
                            }
                        }
                    }
                }

                previousLead1 = lead1;
                previousLead2 = lead2;
                pullbackCounter += 1;
            }
        }
    }

    public void openPositions(bool isBuy) {
        if (isBuy)
        {
            //    decimal tp1 = Math.Abs(box + past24CrossedCandle.High);
            //    decimal tp2 = Math.Abs(box + past24CrossedCandle.High + 3);
            //    decimal tp3 = Math.Abs(box + past24CrossedCandle.High + 3);
            //    decimal sl1 = Math.Abs(box - past24CrossedCandle.Low);
            //    decimal sl2 = Math.Abs(box - past24CrossedCandle.Low - 3);
            //    decimal sl3 = Math.Abs(box - past24CrossedCandle.Low - 3);
            //    MarketOrder(symbol, 3);
            //    StopMarketOrder(symbol, -1.5, sl1);
            //    StopMarketOrder(symbol, -1, sl2);
            //    StopMarketOrder(symbol, -0.5, sl3);
            //    LimitOrder(symbol, -1.5, tp1);
            //    LimitOrder(symbol, -1, tp2);
            //    LimitOrder(symbol, -0.5, tp3);


            //decimal tp1 = Math.Abs(box + past24CrossedCandle.High);
            //decimal sl1 = Math.Abs(box - past24CrossedCandle.Low);

            decimal tp1 = past24CrossedCandle.High + box;
            decimal sl1 = past24CrossedCandle.Low - box;
            MarketOrder(symbol, 1);
            //StopMarketOrder(symbol, -1, sl1);
            //LimitOrder(symbol, -1, tp1);
            //StopLimitOrder(symbol, -1, sl1, tp1);


            TrailingStopOrder(
                symbol: symbol,
                quantity: -1,
                trailingAmount: box * 3,
                trailingAsPercentage: false);

            //if (currentBar.Close > tp1 || currentBar.Close < sl1)
            //{
            //    Liquidate(symbol);
            //}
            //else if (!Portfolio.Invested)
            //{
            //    MarketOrder(symbol, 1);
            //}
        }
        else
        {

            //    decimal tp1 = Math.Abs(box - past24CrossedCandle.High);
            //    decimal tp2 = Math.Abs(box - past24CrossedCandle.High - 3);
            //    decimal tp3 = Math.Abs(box - past24CrossedCandle.High - 3);
            //    decimal sl1 = Math.Abs(box + past24CrossedCandle.Low);
            //    decimal sl2 = Math.Abs(box + past24CrossedCandle.Low + 3);
            //    decimal sl3 = Math.Abs(box + past24CrossedCandle.Low + 3);
            //    MarketOrder(symbol, -3);
            //    StopMarketOrder(symbol, 1.5, sl1);
            //    StopMarketOrder(symbol, 1, sl2);
            //    StopMarketOrder(symbol, 0.5, sl3);
            //    LimitOrder(symbol, 1.5, tp1);
            //    LimitOrder(symbol, 1, tp2);
            //    LimitOrder(symbol, 0.5, tp3);


            //decimal tp1 = Math.Abs(box - past24CrossedCandle.High);
            //decimal sl1 = Math.Abs(box + past24CrossedCandle.Low);

            decimal tp1 = past24CrossedCandle.Low - box;
            decimal sl1 = past24CrossedCandle.High + box;
            MarketOrder(symbol, -1);
            //StopMarketOrder(symbol, 1, sl1);
            //LimitOrder(symbol, 1, tp1);
            //StopLimitOrder(symbol, 1, sl1, tp1);


            TrailingStopOrder(
                symbol: symbol,
                quantity: 1,
                trailingAmount: box * 3,
                trailingAsPercentage: false);

            //if (currentBar.Close < tp1 || currentBar.Close > sl1)
            //{
            //    Liquidate(symbol);
            //}
            //else if (!Portfolio.Invested) {
            //    MarketOrder(symbol, -1);
            //}
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
