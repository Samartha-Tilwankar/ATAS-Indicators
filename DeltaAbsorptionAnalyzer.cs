using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Delta Absorption Analyzer - Analyzes cumulative delta to detect absorption zones
    /// and identifies when institutional players are absorbing market orders
    /// </summary>
    public class DeltaAbsorptionAnalyzer : Indicator
    {
        private readonly List<decimal> _closes = new();
        private readonly List<decimal> _volumes = new();
        private readonly List<decimal> _opens = new();
        private readonly List<decimal> _highs = new();
        private readonly List<decimal> _lows = new();
        
        [Parameter("Accumulation Period", DefaultValue = 15)]
        public int AccumulationPeriod { get; set; } = 15;

        [Parameter("Sensitivity", DefaultValue = 1.5)]
        public double Sensitivity { get; set; } = 1.5;

        [Parameter("Cumulative Delta Period", DefaultValue = 50)]
        public int CumulativeDeltaPeriod { get; set; } = 50;

        private decimal _cumulativeDelta;
        
        public DeltaAbsorptionAnalyzer()
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

            if (bar < 1)
            {
                this[bar] = 0;
                return;
            }

            // Estimate delta based on close position (simplified delta estimation)
            // In real ATAS, you'd use actual bid/ask data
            decimal range = high - low;
            decimal estimatedDelta = 0;
            
            if (range > 0)
            {
                decimal closePosition = (close - low) / range;
                // If close is in top 50% of range, assume buying delta, else selling
                estimatedDelta = (closePosition - 0.5m) * 2 * volume;
            }

            _cumulativeDelta += estimatedDelta;

            if (bar < AccumulationPeriod)
            {
                this[bar] = 0;
                return;
            }

            // Calculate delta trend
            decimal deltaChange = estimatedDelta - GetAverageDelta(bar, AccumulationPeriod);
            
            // Calculate cumulative delta trend
            decimal cumDeltaTrend = _cumulativeDelta - GetCumulativeDelta(bar, CumulativeDeltaPeriod);
            
            // Calculate average volume
            decimal avgVolume = GetAverageVolume(bar, AccumulationPeriod);
            decimal volumeRatio = volume / (avgVolume > 0 ? avgVolume : 1);

            decimal absorptionSignal = 0;

            // Absorption Pattern 1: High volume + flat cumulative delta (absorption)
            if (volumeRatio >= (decimal)Sensitivity && Math.Abs(cumDeltaTrend) < avgVolume * 0.5m)
            {
                absorptionSignal = 50 * volumeRatio;
            }

            // Absorption Pattern 2: Large price rejection with high volume
            if (volumeRatio >= (decimal)Sensitivity)
            {
                bool isUpBar = close > open;
                bool isRejection = isUpBar ? (close - low) < (high - low) * 0.3m : (high - close) < (high - low) * 0.3m;
                
                if (isRejection)
                {
                    absorptionSignal = 75 * volumeRatio * (isUpBar ? -1 : 1);
                }
            }

            // Absorption Pattern 3: Cumulative delta divergence from price
            if (bar > CumulativeDeltaPeriod)
            {
                decimal priceChange = _closes[bar] - _closes[bar - CumulativeDeltaPeriod];
                decimal deltaDivergence = cumDeltaTrend;
                
                // If price moved but delta didn't follow = absorption
                if (Math.Sign(priceChange) != Math.Sign(deltaDivergence) && Math.Abs(priceChange) > 0)
                {
                    absorptionSignal += 40;
                }
            }

            this[bar] = Math.Max(-100, Math.Min(100, absorptionSignal));
        }

        private decimal GetAverageDelta(int bar, int period)
        {
            if (bar < period) return 0;

            decimal sum = 0;
            for (int i = bar - period; i < bar; i++)
            {
                decimal range = _highs[i] - _lows[i];
                if (range > 0)
                {
                    decimal position = (_closes[i] - _lows[i]) / range;
                    sum += (position - 0.5m) * 2 * _volumes[i];
                }
            }
            return sum / period;
        }

        private decimal GetCumulativeDelta(int bar, int period)
        {
            if (bar < period) return 0;

            decimal cumDelta = 0;
            for (int i = bar - period; i < bar; i++)
            {
                decimal range = _highs[i] - _lows[i];
                if (range > 0)
                {
                    decimal position = (_closes[i] - _lows[i]) / range;
                    cumDelta += (position - 0.5m) * 2 * _volumes[i];
                }
            }
            return cumDelta;
        }

        private decimal GetAverageVolume(int bar, int period)
        {
            if (bar < period) return 0;

            decimal sum = 0;
            for (int i = bar - period; i < bar; i++)
                sum += _volumes[i];

            return sum / period;
        }
    }
}
