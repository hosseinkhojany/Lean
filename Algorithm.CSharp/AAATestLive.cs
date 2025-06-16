using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Market; // For Bars
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using QuantConnect.Algorithm.CSharp;
using System.Linq;
using System.Threading;
using MathNet.Numerics;
using QuantConnect.Brokerages;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Statistics;

namespace QuantConnect.Algorithm.CSharp
{
    public class AAATestLive : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol symbol;
        private bool _hasPlacedOrder = false;
        public override void Initialize()
        {
            Settings.DailyPreciseEndTime = false;
            symbol = AddCrypto("BTCUSD", Resolution.Tick).Symbol;
            // symbol = AddCfd("XAUUSD", Resolution.Tick).Symbol;
            SetWarmup(10, Resolution.Tick);
        }


        public override void OnData(Slice slice)
        {
            TradeBar currentBar = new TradeBar();
            if(slice.QuoteBars.Count > 0)
            {
                QuoteBar quoteBar = slice.Get<QuoteBar>(symbol);
                currentBar = new TradeBar
                {
                    Time = quoteBar.Time,
                    Open = quoteBar.Open,
                    High = quoteBar.High,
                    Low = quoteBar.Low,
                    Close = quoteBar.Close,
                    Value = quoteBar.Value,
                    Symbol = quoteBar.Symbol
                };
                
            }

            if (slice.Bars.Count > 0)
            {
                currentBar = slice.Get<TradeBar>().First().Value;
            }

            if (!Portfolio.Invested && !_hasPlacedOrder && !IsWarmingUp && Securities[symbol].Price > 0)
            {
                // OrderTicket order = StopMarketOrder(symbol, -1, stopPrice: Securities[symbol].Price + 50);
                // OrderTicket order = StopLimitOrder(symbol, 
                //     Securities[symbol].SymbolProperties.MinimumOrderSize ?? 0.01m, 
                //     stopPrice: Securities[symbol].Price - 50,
                //     limitPrice: Securities[symbol].Price + 50);
                Security security = Securities[symbol];
                OrderTicket order = MarketOrder(symbol, security.SymbolProperties.MinimumOrderSize ?? 1, tag: "$=11");
                _hasPlacedOrder = true;
                Log($"{Time}: Data confirmed for {symbol}. Price: {currentBar.Price}. Placing Market Order.");
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log($"{Time}: Order Event: {orderEvent.Symbol}, Status: {orderEvent.Status}, Qty: {orderEvent.Quantity}, FillQty: {orderEvent.FillQuantity}, FillPrice: {orderEvent.FillPrice:F5}");
        }


        public override void OnEndOfAlgorithm()
        {
        }
        

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public List<Language> Languages { get; } = new() { Language.CSharp, Language.Python };

        /// <summary>
        /// Data Points count of all timeslices of algorithm
        /// </summary>
        public long DataPoints => 9999;

        /// <summary>
        /// Data Points count of the algorithm history
        /// </summary>
        public int AlgorithmHistoryDataPoints => 9999;

        /// <summary>
        /// Final status of the algorithm
        /// </summary>
        public AlgorithmStatus AlgorithmStatus => AlgorithmStatus.Completed;

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
            {"Total Orders", "0"},
            {"Average Win", "0%"},
            {"Average Loss", "0%"},
            {"Compounding Annual Return", "0%"},
            {"Drawdown", "0%"},
            {"Expectancy", "0"},
            {"Start Equity", "100000"},
            {"End Equity", "100000"},
            {"Net Profit", "0%"},
            {"Sharpe Ratio", "0"},
            {"Sortino Ratio", "0"},
            {"Probabilistic Sharpe Ratio", "0%"},
            {"Loss Rate", "0%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "0"},
            {"Tracking Error", "0"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$0.00"},
            {"Estimated Strategy Capacity", "$0"},
            {"Lowest Capacity Asset", ""},
            {"Portfolio Turnover", "0%"},
            {"OrderListHash", "d41d8cd98f00b204e9800998ecf8427e"}
        };
    }
}
