using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Market; // For Bars
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using QuantConnect.Algorithm.CSharp;
using System.Linq;
using MathNet.Numerics;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Statistics;

namespace QuantConnect.Algorithm.CSharp
{
    public class AAABacktestingSendOrder : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private Symbol _xauusdSymbol;
        private bool _hasPlacedOrder = false;
        public override void Initialize()
        {
            SetStartDate(2025, 1, 1); 
            SetEndDate(2025, 4, 4);
            SetCash(100000);

            Settings.DailyPreciseEndTime = false;
            _xauusdSymbol = AddData<AAADaily>("XAUUSD", Resolution.Minute).Symbol;

            
            var history3 = History<AAADaily>(new[] { _xauusdSymbol }, 1000, Resolution.Minute).ToList();
            int i = HistoryProvider.DataPointCount;
            Log($"History Size: {history3.Count} DataPointCount: {i}");
            
            SetWarmup(10, Resolution.Minute);
        }


        public override void OnData(Slice slice)
        {
            var currentBar = slice.Get<AAADaily>().First().Value;
            if (currentBar.Price == 0)
            {
                Log($"Price is zero for {_xauusdSymbol} at {Time}");
                return;
            }
            if (!Portfolio.Invested && !_hasPlacedOrder && !IsWarmingUp && Securities[_xauusdSymbol].Price > 0)
            {
                
                
                var forex = Securities[_xauusdSymbol];
                var entryBid = forex.BidPrice;
                var entryAsk = forex.AskPrice;
                var pipSize = forex.SymbolProperties.LotSize;
                
                //open order
                OrderTicket order = MarketOrder(_xauusdSymbol, 1);
                _hasPlacedOrder = true;
                Log($"{Time}: Data confirmed for {_xauusdSymbol}. Price: {currentBar.Price}. Placing Market Order.");
            }
            
            var history3 = History<AAADaily>(new[] { _xauusdSymbol }, 1000, Resolution.Minute).ToList();
            int i = HistoryProvider.DataPointCount;
            Log($"History Size: {history3.Count} DataPointCount: {i}");
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            
            Log($"{Time}: Order Event: {orderEvent.Symbol}, Status: {orderEvent.Status}, Qty: {orderEvent.Quantity}, FillQty: {orderEvent.FillQuantity}, FillPrice: {orderEvent.FillPrice:F5}");
        }


        public override void OnEndOfAlgorithm()
        {
            var w = Statistics;
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
