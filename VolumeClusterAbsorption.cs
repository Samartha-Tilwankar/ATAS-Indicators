using System;
using System.Collections.Generic;

namespace ATAS.Indicators
{
    /// <summary>
    /// Volume Cluster Absorption - Identifies price clusters where high volume 
    /// absorption occurs, showing where institutional players are active
    /// </summary>
    public class VolumeClusterAbsorption : Indicator
    {
        private readonly List<decimal> _closes = new();
        private readonly List<decimal> _volumes = new();
        private readonly List<decimal> _highs = new();
        private readonly List<decimal> _lows = new();
        
        [Parameter("Cluster Range %", DefaultValue = 0.5)]
        public double ClusterRangePercent { get; set; } = 0.5;

        [Parameter("Min Volume Multiplier", DefaultValue = 2.0)]
        public double MinVolumeMultiplier { get; set; } = 2.0;

        [Parameter("Lookback Bars", DefaultValue = 30)]
        public int LookbackBars { get; set; } = 30;

        private readonly Dictionary<int, decimal> _clusterVolumes = new();
        
        public VolumeClusterAbsorption()
        {
            Panel = IndicatorDataPanel.NewPanel;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var close = GetSeriesValue(bar, PriceField.Close);
            var high = GetSeriesValue(bar, PriceField.High);
            var low = GetSeriesValue(bar, PriceField.Low);
            var volume = GetSeriesValue(bar, VolumeField);

            _closes.Add(close);
            _highs.Add(high);
            _lows.Add(low);
            _volumes.Add(volume);

            if (bar < LookbackBars)
            {
                this[bar] = 0;
                return;
            }

            // Calculate average volume
            decimal avgVolume = 0;
            for (int i = _volumes.Count - LookbackBars; i < _volumes.Count; i++)
                avgVolume += _volumes[i];
            avgVolume /= LookbackBars;

            decimal thresholdVolume = avgVolume * (decimal)MinVolumeMultiplier;

            // Find cluster for current price
            int currentCluster = GetClusterIndex(bar, close);
            
            // Calculate volume in this cluster
            decimal clusterVolume = 0;
            for (int i = bar - LookbackBars; i <= bar; i++)
            {
                if (i < 0) continue;
                int histCluster = GetClusterIndex(i, _closes[i]);
                if (histCluster == currentCluster)
                    clusterVolume += _volumes[i];
            }

            // Calculate cluster strength
            decimal clusterStrength = 0;
            if (clusterVolume >= thresholdVolume)
            {
                // Calculate how much of total volume is in this cluster
                decimal totalRecentVolume = 0;
                for (int i = bar - LookbackBars; i <= bar; i++)
                {
                    if (i >= 0) totalRecentVolume += _volumes[i];
                }

                decimal concentration = clusterVolume / (totalRecentVolume > 0 ? totalRecentVolume : 1);
                
                // Determine if absorption is bullish or bearish based on price action
                bool isAbsorptionBullish = IsBullishAbsorption(bar, currentCluster);
                bool isAbsorptionBearish = IsBearishAbsorption(bar, currentCluster);

                if (isAbsorptionBullish)
                    clusterStrength = concentration * 100;
                else if (isAbsorptionBearish)
                    clusterStrength = -concentration * 100;
                else
                    clusterStrength = concentration * 50;
            }

            this[bar] = Math.Max(-100, Math.Min(100, clusterStrength));
        }

        private int GetClusterIndex(int bar, decimal price)
        {
            if (bar == 0 || _closes.Count == 0) return 0;
            
            decimal referencePrice = _closes[0];
            decimal clusterSize = referencePrice * (decimal)(ClusterRangePercent / 100);
            
            if (clusterSize == 0) return 0;
            
            return (int)Math.Floor((double)(price / clusterSize));
        }

        private bool IsBullishAbsorption(int bar, int cluster)
        {
            // Check if recent bars in this cluster show buying absorption
            // (price rejected lower but closed higher)
            for (int i = Math.Max(0, bar - 5); i < bar; i++)
            {
                int histCluster = GetClusterIndex(i, _closes[i]);
                if (histCluster == cluster && _volumes[i] > 0)
                {
                    decimal range = _highs[i] - _lows[i];
                    if (range > 0)
                    {
                        decimal closePos = (_closes[i] - _lows[i]) / range;
                        // If closed in top portion, potential bullish absorption
                        if (closePos > 0.6m)
                            return true;
                    }
                }
            }
            return false;
        }

        private bool IsBearishAbsorption(int bar, int cluster)
        {
            // Check if recent bars in this cluster show selling absorption
            for (int i = Math.Max(0, bar - 5); i < bar; i++)
            {
                int histCluster = GetClusterIndex(i, _closes[i]);
                if (histCluster == cluster && _volumes[i] > 0)
                {
                    decimal range = _highs[i] - _lows[i];
                    if (range > 0)
                    {
                        decimal closePos = (_closes[i] - _lows[i]) / range;
                        // If closed in bottom portion, potential bearish absorption
                        if (closePos < 0.4m)
                            return true;
                    }
                }
            }
            return false;
        }
    }
}
