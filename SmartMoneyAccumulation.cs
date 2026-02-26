using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Smart Money Accumulation Detector - Detects institutional accumulation/distribution
    /// patterns using price-volume analysis with multiple timeframe confirmation
    /// </summary>
    public class SmartMoneyAccumulation : Indicator
    {
        private readonly List<decimal> _closes = new();
        private readonly List<decimal> _volumes = new();
        private readonly List<decimal> _opens = new();
        private readonly List<decimal> _highs = new();
        private readonly List<decimal> _lows = new();
        
        [Parameter("Short Period", DefaultValue = 5)]
        public int ShortPeriod { get; set; } = 5;

        [Parameter("Medium Period", DefaultValue = 15)]
        public int MediumPeriod { get; set; } = 15;

        [Parameter("Long Period", DefaultValue = 50)]
        public int LongPeriod { get; set; } = 50;

        [Parameter("Accumulation Threshold", DefaultValue = 0.6)]
        public double AccumulationThreshold { get; set; } = 0.6;

        public SmartMoneyAccumulation()
        {
            Panel = IndicatorDataPanel.NewPanel;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var close = GetSeriesValue(bar, PriceField.Close);
            var open = GetSeriesValue(bar, PriceField.Open);
            var high = GetSeriesValue(bar, PriceField.High);
            var low = GetSeriesValue(bar, PriceField.Low);
            var volume = GetSeriesValue(bar, VolumeField);

            _closes.Add(close);
            _opens.Add(open);
            _highs.Add(high);
            _lows.Add(low);
            _volumes.Add(volume);

            if (bar < LongPeriod)
            {
                this[bar] = 0;
                return;
            }

            // Calculate N-period accumulation/distribution line
            decimal shortAD = CalculateAD(bar, ShortPeriod);
            decimal mediumAD = CalculateAD(bar, MediumPeriod);
            decimal longAD = CalculateAD(bar, LongPeriod);

            // Calculate volume relationship
            decimal avgVolume = GetAverageVolume(bar, MediumPeriod);
            decimal volumeRatio = volume / (avgVolume > 0 ? avgVolume : 1);

            // Smart Money Index: compares short vs long term accumulation
            decimal smi = 0;
            
            // Accumulation confirmed when short > medium > long (positive divergence)
            if (shortAD > mediumAD && mediumAD > longAD)
            {
                smi = 100 * (1 + (shortAD - longAD) / (longAD != 0 ? Math.Abs(longAD) : 1));
            }
            // Distribution confirmed when short < medium < long (negative divergence)
            else if (shortAD < mediumAD && mediumAD < longAD)
            {
                smi = -100 * (1 + (longAD - shortAD) / (longAD != 0 ? Math.Abs(longAD) : 1));
            }
            // No clear trend
            else
            {
                smi = (shortAD - longAD) / (longAD != 0 ? Math.Abs(longAD) : 1) * 50;
            }

            // Apply volume filter - high volume strengthens the signal
            if (volumeRatio > 1.5m)
            {
                smi *= (1 + (volumeRatio - 1) * 0.5m);
            }

            // Additional check: Climb (accumulation) vs Climb (distribution) pattern
            bool isClimbPattern = DetectClimbPattern(bar);
            if (isClimbPattern && smi > 0)
            {
                smi *= 1.5m;
            }

            this[bar] = Math.Max(-100, Math.Min(100, smi));
        }

        private decimal CalculateAD(int bar, int period)
        {
            if (bar < period) return 0;

            decimal totalAD = 0;
            decimal referenceClose = _closes[bar - period];

            for (int i = bar - period; i <= bar; i++)
            {
                decimal range = _highs[i] - _lows[i];
                if (range == 0) continue;

                // Money Flow Index approximation
                decimal closePosition = (_closes[i] - _lows[i]) / range;
                
                // Money Flow Multiplier
                decimal multiplier = 0;
                if (closePosition > 0.75m)
                    multiplier = 1;
                else if (closePosition > 0.25m)
                    multiplier = 0.5m;
                else if (closePosition > 0)
                    multiplier = -0.5m;
                else
                    multiplier = -1;

                // Money Flow Volume
                decimal moneyFlowVolume = multiplier * _volumes[i];
                
                // Accumulation/Distribution line value
                totalAD += moneyFlowVolume;
            }

            return totalAD;
        }

        private decimal GetAverageVolume(int bar, int period)
        {
            if (bar < period) return 0;

            decimal sum = 0;
            for (int i = bar - period; i < bar; i++)
                sum += _volumes[i];

            return sum / period;
        }

        private bool DetectClimbPattern(int bar)
        {
            // "Climb" pattern: Higher highs and higher lows with high volume
            // Indicates smart money accumulating
            if (bar < 5) return false;

            int upBars = 0;
            for (int i = bar - 5; i < bar; i++)
            {
                if (_closes[i] > _opens[i]) // Up bar
                {
                    // Check if volume is above average
                    decimal avgVol = GetAverageVolume(i, 10);
                    if (_volumes[i] > avgVol * 1.2m)
                        upBars++;
                }
            }

            return upBars >= 4;
        }
    }
}
