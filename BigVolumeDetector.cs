using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Big Volume Detector - Identifies bars with significantly above-average volume
    /// </summary>
    public class BigVolumeDetector : Indicator
    {
        private readonly List<double> _volumeHistory = new();
        private int _period = 20;
        
        [Parameter("Period", DefaultValue = 20)]
        public int Period
        {
            get => _period;
            set
            {
                _period = value;
                RecalculateValues();
            }
        }

        [Parameter("Multiplier", DefaultValue = 2.0)]
        public double Multiplier { get; set; } = 2.0;

        public BigVolumeDetector()
        {
            Panel = IndicatorDataPanel.NewPanel;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var currentVolume = GetSeriesValue(bar, VolumeField);
            
            _volumeHistory.Add((double)currentVolume);
            if (_volumeHistory.Count > _period * 2)
                _volumeHistory.RemoveAt(0);

            if (_volumeHistory.Count < _period)
            {
                this[bar] = 0;
                return;
            }

            // Calculate average volume
            double sum = 0;
            for (int i = _volumeHistory.Count - _period; i < _volumeHistory.Count; i++)
                sum += _volumeHistory[i];
            
            double avgVolume = sum / _period;
            double threshold = avgVolume * Multiplier;

            // Return normalized value (0-100 scale)
            if ((double)currentVolume >= threshold)
            {
                // Higher value = more significant the volume spike
                this[bar] = (decimal)Math.Min(100, ((double)currentVolume / threshold) * 50);
            }
            else
            {
                this[bar] = 0;
            }
        }
    }
}
