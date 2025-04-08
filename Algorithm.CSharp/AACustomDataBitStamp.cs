/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Indicators.CandlestickPatterns;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    public class AACustomDataBitStamp : QCAlgorithm
    {
        private Symbol symbol = QuantConnect.Symbol.Create("XAUUSD", SecurityType.Equity, Market.USA);
        private Engulfing _engulfing;

        public override void Initialize()
        {
            SetStartDate(2014, 5, 01); 
            SetEndDate(2014, 5, 14);  
            SetCash(100000);          
            AddCfd("XAUUSD", Resolution.Minute);
        }

        public override void OnData(Slice slice)
        {
            Debug("OnData");
            // if (_engulfing.IsReady)
            // {
            //     Plot("Engulfing", "engulfing", _engulfing);
            //     Console.WriteLine("Engulf: "+_engulfing.ToString());
            // }
            // if (!Portfolio.Invested)
            // {
            //     SetHoldings(symbol, 1);
            //     Debug("Purchased Stock");
            // }
        }

        public bool CanRunLocally { get; } = true;

        public List<Language> Languages { get; } = new() { Language.CSharp, Language.Python };

    }
}
