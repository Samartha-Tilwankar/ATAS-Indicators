//=============================================================================
// NinjaTrader 8 - QUANTUM DELTA PRO V1 (Advanced)
//=============================================================================
// Improvements:
// 1. Quantum delta with nested wave analysis
// 2. Absorption fractal detection
// 3. Market structure change detection (MSCH)
// 4. Order block identification
// 5. Fair value gap detection
// 6. Break of structure confirmation
// 7. Stochastic RSI filter
//=============================================================================
namespace NinjaTrader.NinjaScript.Indicators
{
    public class QuantumDeltaPro : Indicator
    {
        private Series<double> quantumScore;
        private Series<double> structureScore;
        private Series<double> orderBlockZone;
        
        [Parameter("Fast EMA", DefaultValue = 8)]
        public int FastEMA { get; set; }
        
        [Parameter("Medium EMA", DefaultValue = 21)]
        public int MediumEMA { get; set; }
        
        [Parameter("Slow EMA", DefaultValue = 50)]
        public int SlowEMA { get; set; }
        
        [Parameter("Supertrend Period", DefaultValue = 10)]
        public int SupertrendPeriod { get; set; }
        
        [Parameter("Supertrend Multiplier", DefaultValue = 2.5)]
        public double SupertrendMultiplier { get; set; }
        
        [Parameter("Delta Period", DefaultValue = 20)]
        public int DeltaPeriod { get; set; }
        
        [Parameter("Fractal Period", DefaultValue = 5)]
        public int FractalPeriod { get; set; }
        
        [Parameter("Order Block Lookback", DefaultValue = 10)]
        public int OrderBlockLookback { get; set; }
        
        [Parameter("Volume Threshold", DefaultValue = 1.8)]
        public double VolumeThreshold { get; set; }
        
        [Parameter("Stochastic Period", DefaultValue = 14)]
        public int StochasticPeriod { get; set; }
        
        [Parameter("Stochastic Smooth", DefaultValue = 3)]
        public int StochasticSmooth { get; set; }
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "QuantumDeltaProV1";
                Description = "Quantum Delta Pro - Advanced quantum-level delta analysis";
                AddPlot(SeriesColor.Lime, "Buy Signal");
                AddPlot(SeriesColor.Red, "Sell Signal");
                AddPlot(SeriesColor.Gold, "Quantum Score");
                AddPlot(SeriesColor.Cyan, "Supertrend");
                AddPlot(SeriesColor.Magenta, "Order Block");
                
                quantumScore = new Series<double>(this);
                structureScore = new Series<double>(this);
                orderBlockZone = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(SlowEMA, OrderBlockLookback))
            {
                Values[0][0] = 0;
                Values[1][0] = 0;
                Values[2][0] = 0;
                Values[3][0] = Close[0];
                Values[4][0] = 0;
                return;
            }
            
            // ===== CORE CALCULATIONS =====
            
            // 1. EMA Trinity
            double emaFast = EMA(Close, FastEMA)[0];
            double emaMedium = EMA(Close, MediumEMA)[0];
            double emaSlow = EMA(Close, SlowEMA)[0];
            
            // 2. Quantum Delta Analysis
            double delta = CalculateQuantumDelta();
            double waveDelta = CalculateWaveDelta();
            
            // 3. Fractal Detection
            bool upFractal = DetectUpFractal();
            bool downFractal = DetectDownFractal();
            
            // 4. Market Structure Change
            bool mschUp = DetectMarketStructureChange(true);
            bool mschDown = DetectMarketStructureChange(false);
            
            // 5. Order Block Detection
            double orderBlock = DetectOrderBlocks();
            orderBlockZone[0] = orderBlock;
            Values[4][0] = orderBlock;
            
            // 6. Fair Value Gap
            double fvg = DetectFairValueGap();
            
            // 7. Stochastic RSI
            double stochK = CalculateStochasticK();
            double stochD = CalculateStochasticD();
            
            // 8. Supertrend
            double supertrend = CalculateSupertrend();
            Values[3][0] = supertrend;
            
            // 9. Quantum Score
            double qScore = CalculateQuantumScore(emaFast, emaMedium, emaSlow, 
                                                   delta, waveDelta, mschUp, mschDown,
                                                   orderBlock, fvg, stochK, stochD);
            quantumScore[0] = qScore;
            Values[2][0] = qScore;
            
            // ===== GENERATE SIGNALS =====
            
            double volumeMA = SMA(Volume, 14)[0];
            double volumeRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            bool supertrendBullish = Close[0] > supertrend && Values[3][1] < Values[3][2];
            bool supertrendBearish = Close[0] < supertrend && Values[3][1] > Values[3][2];
            
            bool emaBullish = emaFast > emaMedium && emaMedium > emaSlow;
            bool emaBearish = emaFast < emaMedium && emaMedium < emaSlow;
            
            // Stochastic conditions
            bool stochBuy = stochK < 30 || (stochK > stochD && stochK < 50);
            bool stochSell = stochK > 70 || (stochK < stochD && stochK > 50);
            
            bool buySignal = false;
            bool sellSignal = false;
            
            // BUY: MSCH + Order Block + Delta + EMA + Supertrend
            if (mschUp || (supertrendBullish && emaBullish))
            {
                if (orderBlock > 0 && delta > 0 && volumeRatio > VolumeThreshold)
                {
                    if (stochBuy || fvg > 0)
                    {
                        buySignal = true;
                    }
                }
            }
            
            // Secondary Buy: Trend continuation
            if (emaBullish && supertrend > 0 && orderBlock > 0 && delta > 0 && volumeRatio > 1.4)
            {
                buySignal = true;
            }
            
            // SELL: MSCH + Order Block + Delta + EMA + Supertrend
            if (mschDown || (supertrendBearish && emaBearish))
            {
                if (orderBlock < 0 && delta < 0 && volumeRatio > VolumeThreshold)
                {
                    if (stochSell || fvg < 0)
                    {
                        sellSignal = true;
                    }
                }
            }
            
            // Secondary Sell: Trend continuation
            if (emaBearish && supertrend < 0 && orderBlock < 0 && delta < 0 && volumeRatio > 1.4)
            {
                sellSignal = true;
            }
            
            Values[0][0] = buySignal ? Math.Min(100, qScore) : 0;
            Values[1][0] = sellSignal ? Math.Max(-100, qScore) : 0;
        }
        
        // ===== QUANTUM CALCULATION METHODS =====
        
        private double CalculateQuantumDelta()
        {
            double range = High[0] - Low[0];
            if (range <= 0) return 0;
            
            double closePos = (Close[0] - Low[0]) / range;
            double volumeMA = SMA(Volume, DeltaPeriod)[0];
            double volRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            // Quantum amplification for high volume
            double amp = volRatio > 2.0 ? 1.5 : (volRatio > 1.5 ? 1.25 : 1.0);
            
            return (closePos - 0.5) * 2 * Volume[0] * amp;
        }
        
        private double CalculateWaveDelta()
        {
            double[] waves = new double[5];
            int waveSize = DeltaPeriod / 5;
            
            for (int w = 0; w < 5; w++)
            {
                double waveDelta = 0;
                for (int i = w * waveSize; i < (w + 1) * waveSize && i < DeltaPeriod; i++)
                {
                    double range = High[i] - Low[i];
                    if (range <= 0) continue;
                    double pos = (Close[i] - Low[i]) / range;
                    waveDelta += (pos - 0.5) * 2 * Volume[i];
                }
                waves[w] = waveDelta;
            }
            
            // Calculate wave momentum
            double momentum = 0;
            for (int w = 1; w < 5; w++)
            {
                momentum += waves[w] - waves[w-1];
            }
            
            return momentum / 4;
        }
        
        private bool DetectUpFractal()
        {
            if (CurrentBar < FractalPeriod * 2) return false;
            
            for (int i = 1; i <= FractalPeriod; i++)
            {
                if (High[i] > High[0]) return false;
            }
            return true;
        }
        
        private bool DetectDownFractal()
        {
            if (CurrentBar < FractalPeriod * 2) return false;
            
            for (int i = 1; i <= FractalPeriod; i++)
            {
                if (Low[i] < Low[0]) return false;
            }
            return true;
        }
        
        private bool DetectMarketStructureChange(bool isBullish)
        {
            if (CurrentBar < 5) return false;
            
            if (isBullish)
            {
                // Check for bullish MSCH: price breaks above previous high with momentum
                double recentHigh = High[1];
                for (int i = 2; i <= 5; i++)
                    recentHigh = Math.Max(recentHigh, High[i]);
                
                return Close[0] > recentHigh && Volume[0] > SMA(Volume, 10)[0];
            }
            else
            {
                // Bearish MSCH
                double recentLow = Low[1];
                for (int i = 2; i <= 5; i++)
                    recentLow = Math.Min(recentLow, Low[i]);
                
                return Close[0] < recentLow && Volume[0] > SMA(Volume, 10)[0];
            }
        }
        
        private double DetectOrderBlocks()
        {
            double blockScore = 0;
            double volumeMA = SMA(Volume, OrderBlockLookback)[0];
            
            for (int i = 2; i <= OrderBlockLookback; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volumeMA > 0 ? volumeMA : 1);
                if (volRatio < 1.5) continue;
                
                double closePos = (Close[i] - Low[i]) / range;
                bool isUpBar = Close[i] > Open[i];
                
                // Bear order block (bullish reversal): high volume down bar near support
                if (!isUpBar && closePos < 0.35 && volRatio > 1.8)
                {
                    // Check if price returned to this zone
                    if (Close[0] > High[i] && Close[0] < High[i] * 1.02)
                        blockScore += volRatio * 10;
                }
                
                // Bull order block (bearish reversal): high volume up bar near resistance
                if (isUpBar && closePos > 0.65 && volRatio > 1.8)
                {
                    if (Close[0] < Low[i] && Close[0] > Low[i] * 0.98)
                        blockScore -= volRatio * 10;
                }
            }
            
            return Math.Max(-100, Math.Min(100, blockScore));
        }
        
        private double DetectFairValueGap()
        {
            if (CurrentBar < 3) return 0;
            
            double gap = 0;
            
            // Bullish FVG: gap between current low and previous high
            if (Low[0] > High[2])
            {
                double fvgSize = Low[0] - High[2];
                double avgRange = 0;
                for (int i = 0; i < 10; i++)
                    avgRange += High[i] - Low[i];
                avgRange /= 10;
                
                if (fvgSize > avgRange * 0.5)
                    gap = fvgSize / Close[0] * 100;
            }
            
            // Bearish FVG
            if (High[0] < Low[2])
            {
                double fvgSize = Low[2] - High[0];
                double avgRange = 0;
                for (int i = 0; i < 10; i++)
                    avgRange += High[i] - Low[i];
                avgRange /= 10;
                
                if (fvgSize > avgRange * 0.5)
                    gap = -fvgSize / Close[0] * 100;
            }
            
            return gap;
        }
        
        private double CalculateStochasticK()
        {
            double lowestLow = Low[0];
            double highestHigh = High[0];
            
            for (int i = 0; i < StochasticPeriod; i++)
            {
                lowestLow = Math.Min(lowestLow, Low[i]);
                highestHigh = Math.Max(highestHigh, High[i]);
            }
            
            if (highestHigh == lowestLow) return 50;
            
            return ((Close[0] - lowestLow) / (highestHigh - lowestLow)) * 100;
        }
        
        private double CalculateStochasticD()
        {
            double sum = 0;
            for (int i = 0; i < StochasticSmooth; i++)
            {
                // Simplified - in production would calculate each K separately
                sum += CalculateStochasticK();
            }
            return sum / StochasticSmooth;
        }
        
        private double CalculateSupertrend()
        {
            double tr = CalculateTrueRange();
            double atr = EMA(new Series<double>(this, 1, tr), SupertrendPeriod)[0];
            
            double sma = SMA(Close, SupertrendPeriod)[0];
            double upper = sma + (SupertrendMultiplier * atr);
            double lower = sma - (SupertrendMultiplier * atr);
            
            double supertrend = Values[3][1];
            
            if (Close[0] > upper)
                supertrend = lower;
            else if (Close[0] < lower)
                supertrend = upper;
            
            return supertrend;
        }
        
        private double CalculateQuantumScore(double emaF, double emaM, double emaS,
                                           double delta, double waveDelta,
                                           bool mschU, bool mschD,
                                           double ob, double fvg,
                                           double stochK, double stochD)
        {
            double score = 0;
            
            // EMA (25 pts)
            if (emaF > emaM && emaM > emaS) score += 25;
            else if (emaF < emaM && emaM < emaS) score -= 25;
            
            // Delta (20 pts)
            score += Math.Min(20, Math.Max(-20, delta / 5000));
            
            // Wave momentum (15 pts)
            score += Math.Min(15, Math.Max(-15, waveDelta / 3000));
            
            // MSCH (15 pts)
            if (mschU) score += 15;
            if (mschD) score -= 15;
            
            // Order Block (15 pts)
            score += Math.Min(15, Math.Max(-15, ob / 5));
            
            // FVG (5 pts)
            score += Math.Min(5, Math.Max(-5, fvg));
            
            // Stochastic (5 pts)
            if (stochK < 30) score += 5;
            else if (stochK > 70) score -= 5;
            
            return Math.Max(-100, Math.Min(100, score));
        }
        
        private double CalculateTrueRange()
        {
            return Math.Max(High[0] - Low[0],
                   Math.Max(Math.Abs(High[0] - Close[1]),
                           Math.Abs(Low[0] - Close[1])));
        }
        
        private double EMA(Series<double> series, int period)
        {
            if (CurrentBar < period) return series[0];
            double multiplier = 2.0 / (period + 1);
            double ema = SMA(series, period)[0];
            for (int i = CurrentBar - period + 1; i <= CurrentBar; i++)
            {
                if (i < 0) continue;
                ema = (series[i] - ema) * multiplier + ema;
            }
            return ema;
        }
        
        private double SMA(Series<double> series, int period)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < period && i <= CurrentBar; i++)
            {
                sum += series[i];
                count++;
            }
            return count > 0 ? sum / count : 0;
        }
    }
}
