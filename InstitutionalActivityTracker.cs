using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Institutional Activity Tracker - Tracks institutional buying/selling pressure
    /// using a combination of volume, price range, and closing position analysis
    /// </summary>
    public class InstitutionalActivityTracker : Indicator
    {
        private readonly List<decimal> _closes = new();
        private readonly List<decimal> _volumes = new();
        private readonly List<decimal> _opens = new();
        private readonly List<decimal> _highs = new();
        private readonly List<decimal> _lows = new();
        
        [Parameter("Fast Period", DefaultValue = 5)]
        public int FastPeriod { get; set; } = 5;

        [Parameter("Slow Period", DefaultValue = 20)]
        public int SlowPeriod { get; set; } = 20;

        [Parameter("Volume Weight", DefaultValue = 0.6)]
        public double VolumeWeight { get; set; } = 0.6;

        public InstitutionalActivityTracker()
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

            if (bar < SlowPeriod)
            {
                this[bar] = 0;
                return;
            }

            // Calculate fast and slow volume-weighted price momentum
            decimal fastPressure = CalculatePressure(bar, FastPeriod);
            decimal slowPressure = CalculatePressure(bar, SlowPeriod);

            // Calculate volume surge
            decimal avgVolumeSlow = GetAverageVolume(bar, SlowPeriod);
            decimal avgVolumeFast = GetAverageVolume(bar, FastPeriod);
            decimal volumeRatio = avgVolumeFast / (avgVolumeSlow > 0 ? avgVolumeSlow : 1);

            // Combined institutional score
            decimal institutionalScore = 0;

            // Strong buying pressure: fast > slow and volume surge
            if (fastPressure > slowPressure && fastPressure > 0 && volumeRatio > 1.5m)
            {
                institutionalScore = fastPressure * (1 + (decimal)VolumeWeight * (volumeRatio - 1));
            }
            // Strong selling pressure: fast < slow and negative
            else if (fastPressure < slowPressure && fastPressure < 0 && volumeRatio > 1.5m)
            {
                institutionalScore = fastPressure * (1 + (decimal)VolumeWeight * (volumeRatio - 1));
            }
            // Normal conditions - use slow pressure
            else
            {
                institutionalScore = slowPressure;
            }

            this[bar] = Math.Max(-100, Math.Min(100, institutionalScore));
        }

        private decimal CalculatePressure(int bar, int period)
        {
            if (bar < period) return 0;

            decimal totalWeight = 0;
            decimal weightedPressure = 0;

            for (int i = _closes.Count - period; i < _closes.Count; i++)
            {
                decimal volume = _volumes[i];
                decimal range = _highs[i] - _lows[i];
                
                if (range == 0) continue;

                // Calculate how far close is from low (0-1 scale, 0.5 = neutral)
                decimal position = (_closes[i] - _lows[i]) / range;

                // Volume-weighted momentum
                decimal momentum = (position - 0.5m) * 2; // -1 to 1 scale
                
                weightedPressure += momentum * volume;
                totalWeight += volume;
            }

            if (totalWeight == 0) return 0;
            return (weightedPressure / totalWeight) * 100;
        }

        private decimal GetAverageVolume(int bar, int period)
        {
            if (bar < period) return 0;

            decimal sum = 0;
            for (int i = _volumes.Count - period; i < _volumes.Count; i++)
                sum += _volumes[i];

            return sum / period;
        }
    }
}
