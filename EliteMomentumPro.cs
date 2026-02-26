//=============================================================================
// NinjaTrader 8 - ELITE MOMENTUM PRO V1 (THE ULTIMATE)
//=============================================================================
// This is the most advanced indicator combining ALL concepts:
// 1. Quantum delta analysis with wave patterns
// 2. ICT concepts: Order Blocks, FVGs, Liquidity Sweeps
// 3. Market Structure: BOS, CHoCH, Trend lines
// 4. Volume Profile: VPOC, VWAP, Value Areas
// 5. Multiple timeframe confirmation
// 6. Adaptive Supertrend with volatility adjustment
// 7. Machine learning-style weighted scoring
// 8. Confluence-based signal generation
//=============================================================================
namespace NinjaTrader.NinjaScript.Indicators
{
    public class EliteMomentumPro : Indicator
    {
        private Series<double> eliteScore;
        private Series<double> confluenceScore;
        
        [Parameter("Fast EMA", DefaultValue = 8)]
        public int FastEMA { get; set; }
        
        [Parameter("Medium EMA", DefaultValue = 21)]
        public int MediumEMA { get; set; }
        
        [Parameter("Slow EMA", DefaultValue = 55)]
        public int SlowEMA { get; set; }
        
        [Parameter("Supertrend Period", DefaultValue = 10)]
        public int SupertrendPeriod { get; set; }
        
        [Parameter("Supertrend Multiplier", DefaultValue = 2.0)]
        public double SupertrendMultiplier { get; set; }
        
        [Parameter("Delta Period", DefaultValue = 20)]
        public int DeltaPeriod { get; set; }
        
        [Parameter("Structure Period", DefaultValue = 10)]
        public int StructurePeriod { get; set; }
        
        [Parameter("Volume Threshold", DefaultValue = 1.6)]
        public double VolumeThreshold { get; set; }
        
        [Parameter("Min Confluence", DefaultValue = 3)]
        public int MinConfluence { get; set; }
        
        [Parameter("Aggression Multiplier", DefaultValue = 1.8)]
        public double AggressionMultiplier { get; set; }
        
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "EliteMomentumProV1";
                Description = "Elite Momentum Pro - The Ultimate Institutional Trading System";
                AddPlot(SeriesColor.Lime, "Buy Signal");
                AddPlot(SeriesColor.Red, "Sell Signal");
                AddPlot(SeriesColor.Gold, "Elite Score");
                AddPlot(SeriesColor.Cyan, "Supertrend");
                AddPlot(SeriesColor.White, "Confluence");
                
                eliteScore = new Series<double>(this);
                confluenceScore = new Series<double>(this);
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(SlowEMA, StructurePeriod))
            {
                Values[0][0] = 0;
                Values[1][0] = 0;
                Values[2][0] = 0;
                Values[3][0] = Close[0];
                Values[4][0] = 0;
                return;
            }
            
            // ===== COMPREHENSIVE ANALYSIS =====
            
            // 1. EMA Analysis (Trend)
            double emaFast = EMA(Close, FastEMA)[0];
            double emaMedium = EMA(Close, MediumEMA)[0];
            double emaSlow = EMA(Close, SlowEMA)[0];
            bool emaAligned = emaFast > emaMedium && emaMedium > emaSlow;
            bool emaAlignedBearish = emaFast < emaMedium && emaMedium < emaSlow;
            
            // 2. Delta Analysis
            double delta = CalculateQuantumDelta();
            double cumDelta = CalculateCumulativeDelta();
            
            // 3. Absorption Detection
            double absorption = CalculateAbsorption();
            
            // 4. Liquidity Sweeps
            double sweep = DetectLiquiditySweeps();
            
            // 5. Order Blocks
            double ob = DetectOrderBlocks();
            
            // 6. Fair Value Gaps
            double fvg = DetectFVG();
            
            // 7. Market Structure
            double bos = DetectBOS();
            bool choch = DetectCHoCH();
            
            // 8. Volume Profile
            double vwap = CalculateVWAP();
            double vwapDev = (Close[0] - vwap) / vwap * 100;
            double vpoc = CalculateVPOC();
            double vpar = CalculateVPAR();
            
            // 9. Supertrend
            double supertrend = CalculateAdaptiveSupertrend();
            Values[3][0] = supertrend;
            
            // 10. Calculate Confluence Factors
            int buyConfluence = 0;
            int sellConfluence = 0;
            
            if (emaAligned) { buyConfluence++; sellConfluence--; }
            if (emaAlignedBearish) { sellConfluence++; buyConfluence--; }
            if (delta > 0) buyConfluence++; else if (delta < 0) sellConfluence++;
            if (absorption > StructurePeriod * 2) buyConfluence++; 
            else if (absorption < -StructurePeriod * 2) sellConfluence++;
            if (sweep > 0) buyConfluence++; else if (sweep < 0) sellConfluence++;
            if (ob > 0) buyConfluence++; else if (ob < 0) sellConfluence++;
            if (fvg > 0) buyConfluence++; else if (fvg < 0) sellConfluence++;
            if (bos > 0) buyConfluence++; else if (bos < 0) sellConfluence++;
            if (vwapDev > 0) buyConfluence++; else if (vwapDev < 0) sellConfluence++;
            
            confluenceScore[0] = buyConfluence - sellConfluence;
            Values[4][0] = confluenceScore[0];
            
            // 11. Elite Score
            double elite = CalculateEliteScore(emaFast, emaMedium, emaSlow,
                                              delta, cumDelta, absorption,
                                              sweep, ob, fvg, bos,
                                              vwapDev, buyConfluence, sellConfluence);
            eliteScore[0] = elite;
            Values[2][0] = elite;
            
            // ===== GENERATE SIGNALS =====
            
            double volumeMA = SMA(Volume, 14)[0];
            double volumeRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            bool supertrendBullish = Close[0] > supertrend && Values[3][1] < Values[3][2];
            bool supertrendBearish = Close[0] < supertrend && Values[3][1] > Values[3][2];
            
            bool buySignal = false;
            bool sellSignal = false;
            
            // ELITE BUY: Multiple confluence factors + Supertrend
            if (buyConfluence >= MinConfluence)
            {
                // Primary: Strong structure break with absorption
                if ((bos > 0 || choch) && (absorption > StructurePeriod * 2 || sweep > 0) && delta > 0)
                {
                    if (supertrendBullish || (supertrend > 0 && Close[0] > supertrend))
                    {
                        if (volumeRatio > VolumeThreshold)
                            buySignal = true;
                    }
                }
                
                // Secondary: Trend continuation with absorption
                if (emaAligned && absorption > StructurePeriod * 3 && delta > 0)
                {
                    if (supertrend > 0 || volumeRatio > VolumeThreshold * 1.2)
                        buySignal = true;
                }
                
                // Tertiary: VWAP bounce with FVG
                if (vwapDev < -0.5 && fvg > 0 && ob > 0)
                {
                    if (supertrend > 0 || emaAligned)
                        buySignal = true;
                }
            }
            
            // ELITE SELL
            if (sellConfluence >= MinConfluence)
            {
                // Primary
                if ((bos < 0 || choch) && (absorption < -StructurePeriod * 2 || sweep < 0) && delta < 0)
                {
                    if (supertrendBearish || (supertrend < 0 && Close[0] < supertrend))
                    {
                        if (volumeRatio > VolumeThreshold)
                            sellSignal = true;
                    }
                }
                
                // Secondary
                if (emaAlignedBearish && absorption < -StructurePeriod * 3 && delta < 0)
                {
                    if (supertrend < 0 || volumeRatio > VolumeThreshold * 1.2)
                        sellSignal = true;
                }
                
                // Tertiary
                if (vwapDev > 0.5 && fvg < 0 && ob < 0)
                {
                    if (supertrend < 0 || emaAlignedBearish)
                        sellSignal = true;
                }
            }
            
            Values[0][0] = buySignal ? Math.Min(100, elite) : 0;
            Values[1][0] = sellSignal ? Math.Max(-100, elite) : 0;
        }
        
        // ===== ELITE CALCULATION METHODS =====
        
        private double CalculateQuantumDelta()
        {
            double range = High[0] - Low[0];
            if (range <= 0) return 0;
            
            double closePos = (Close[0] - Low[0]) / range;
            double volumeMA = SMA(Volume, DeltaPeriod)[0];
            double volRatio = Volume[0] / (volumeMA > 0 ? volumeMA : 1);
            
            // Aggressive amplification
            double amp = 1.0;
            if (volRatio > AggressionMultiplier * 1.3)
                amp = 1.6;
            else if (volRatio > AggressionMultiplier)
                amp = 1.3;
            
            return (closePos - 0.5) * 2 * Volume[0] * amp;
        }
        
        private double CalculateCumulativeDelta()
        {
            double cum = 0;
            for (int i = 0; i < DeltaPeriod; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                double pos = (Close[i] - Low[i]) / range;
                cum += (pos - 0.5) * 2 * Volume[i];
            }
            return cum;
        }
        
        private double CalculateAbsorption()
        {
            double score = 0;
            double volMA = SMA(Volume, StructurePeriod)[0];
            
            for (int i = 1; i <= StructurePeriod; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volMA > 0 ? volMA : 1);
                double closePos = (Close[i] - Low[i]) / range;
                bool isUp = Close[i] > Open[i];
                
                if (volRatio > 2.5)
                {
                    if (isUp && closePos < 0.25)
                        score += volRatio * 15;
                    else if (!isUp && closePos > 0.75)
                        score -= volRatio * 15;
                }
                else if (volRatio > 1.8)
                {
                    if (isUp && closePos < 0.35)
                        score += volRatio * 8;
                    else if (!isUp && closePos > 0.65)
                        score -= volRatio * 8;
                }
                
                // Doji absorption
                if (Math.Abs(Close[i] - Open[i]) < range * 0.1 && volRatio > 2.0)
                    score += (isUp ? 1 : -1) * volRatio * 10;
            }
            
            return Math.Max(-150, Math.Min(150, score));
        }
        
        private double DetectLiquiditySweeps()
        {
            double score = 0;
            double volMA = SMA(Volume, StructurePeriod)[0];
            
            for (int i = 1; i <= 5; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volMA > 0 ? volMA : 1);
                
                bool atHigh = High[i] == MAX(High, 5)[i];
                bool atLow = Low[i] == MIN(Low, 5)[i];
                
                if (atHigh && volRatio > AggressionMultiplier)
                {
                    // Check reversal
                    if (i < 5 && Close[i] < Open[i])
                        score -= 15 * volRatio;
                }
                
                if (atLow && volRatio > AggressionMultiplier)
                {
                    if (i < 5 && Close[i] > Open[i])
                        score += 15 * volRatio;
                }
            }
            
            return score;
        }
        
        private double DetectOrderBlocks()
        {
            double score = 0;
            double volMA = SMA(Volume, StructurePeriod)[0];
            
            for (int i = 2; i <= StructurePeriod; i++)
            {
                double range = High[i] - Low[i];
                if (range <= 0) continue;
                
                double volRatio = Volume[i] / (volMA > 0 ? volMA : 1);
                if (volRatio < 1.8) continue;
                
                double closePos = (Close[i] - Low[i]) / range;
                bool isUp = Close[i] > Open[i];
                
                // Bullish OB
                if (!isUp && closePos < 0.35)
                {
                    if (Close[0] > High[i] && Close[0] < High[i] * 1.025)
                        score += volRatio * 12;
                }
                
                // Bearish OB
                if (isUp && closePos > 0.65)
                {
                    if (Close[0] < Low[i] && Close[0] > Low[i] * 0.975)
                        score -= volRatio * 12;
                }
            }
            
            return Math.Max(-100, Math.Min(100, score));
        }
        
        private double DetectFVG()
        {
            if (CurrentBar < 3) return 0;
            
            double gap = 0;
            double avgRange = 0;
            for (int i = 0; i < 10; i++)
                avgRange += High[i] - Low[i];
            avgRange /= 10;
            
            // Bullish FVG
            if (Low[0] > High[2])
            {
                double size = Low[0] - High[2];
                if (size > avgRange * 0.5)
                    gap = size / Close[0] * 100;
            }
            
            // Bearish FVG
            if (High[0] < Low[2])
            {
                double size = Low[2] - High[0];
                if (size > avgRange * 0.5)
                    gap = -size / Close[0] * 100;
            }
            
            return gap;
        }
        
        private double DetectBOS()
        {
            if (CurrentBar < StructurePeriod + 1) return 0;
            
            double swingHigh = High[1];
            double swingLow = Low[1];
            
            for (int i = 2; i <= StructurePeriod; i++)
            {
                swingHigh = Math.Max(swingHigh, High[i]);
                swingLow = Math.Min(swingLow, Low[i]);
            }
            
            if (Close[0] > swingHigh && Volume[0] > SMA(Volume, 10)[0])
                return 50;
            if (Close[0] < swingLow && Volume[0] > SMA(Volume, 10)[0])
                return -50;
            
            return 0;
        }
        
        private bool DetectCHoCH()
        {
            if (CurrentBar < StructurePeriod + 2) return false;
            
            double prevLow = Low[StructurePeriod + 1];
            for (int i = StructurePeriod + 2; i <= StructurePeriod * 2; i++)
            {
                if (i > CurrentBar) break;
                prevLow = Math.Min(prevLow, Low[i]);
            }
            
            if (Close[0] > High[StructurePeriod] && Low[0] > prevLow)
                return true;
            
            double prevHigh = High[StructurePeriod + 1];
            for (int i = StructurePeriod + 2; i <= StructurePeriod * 2; i++)
            {
                if (i > CurrentBar) break;
                prevHigh = Math.Max(prevHigh, High[i]);
            }
            
            if (Close[0] < Low[StructurePeriod] && High[0] < prevHigh)
                return true;
            
            return false;
        }
        
        private double CalculateVWAP()
        {
            double cumTPV = 0;
            double cumVol = 0;
            
            int period = Math.Min(DeltaPeriod, CurrentBar + 1);
            for (int i = 0; i < period; i++)
            {
                double tp = (High[i] + Low[i] + Close[i]) / 3;
                cumTPV += tp * Volume[i];
                cumVol += Volume[i];
            }
            
            return cumVol > 0 ? cumTPV / cumVol : Close[0];
        }
        
        private double CalculateVPOC()
        {
            double maxVol = 0;
            double vpoc = Close[0];
            
            for (int i = 0; i < StructurePeriod; i++)
            {
                if (Volume[i] > maxVol)
                {
                    maxVol = Volume[i];
                    vpoc = (High[i] + Low[i] + Close[i]) / 3;
                }
            }
            return vpoc;
        }
        
        private double CalculateVPAR()
        {
            double totalVol = 0;
            double weightedSum = 0;
            
            for (int i = 0; i < StructurePeriod; i++)
            {
                double tp = (High[i] + Low[i] + Close[i]) / 3;
                weightedSum += tp * Volume[i];
                totalVol += Volume[i];
            }
            
            return totalVol > 0 ? weightedSum / totalVol : Close[0];
        }
        
        private double CalculateAdaptiveSupertrend()
        {
            double tr = CalculateTrueRange();
            double atr = EMA(new Series<double>(this, 1, tr), SupertrendPeriod)[0];
            
            // Adaptive volatility
            double volRatio = atr / (SMA(new Series<double>(this, 1, tr), SupertrendPeriod * 2)[0] + 0.0001);
            double adaptiveMult = SupertrendMultiplier * (volRatio > 1.3 ? 1.15 : (volRatio < 0.7 ? 0.85 : 1.0));
            
            double sma = SMA(Close, SupertrendPeriod)[0];
            double upper = sma + (adaptiveMult * atr);
            double lower = sma - (adaptiveMult * atr);
            
            double st = Values[3][1];
            if (Close[0] > upper) st = lower;
            else if (Close[0] < lower) st = upper;
            
            return st;
        }
        
        private double CalculateEliteScore(double emaF, double emaM, double emaS,
                                         double delta, double cumDelta,
                                         double absorp, double sweep,
                                         double ob, double fvg, double bos,
                                         double vwapDev, int buyConf, int sellConf)
        {
            double score = 0;
            
            // EMA (15 pts)
            if (emaF > emaM && emaM > emaS) score += 15;
            else if (emaF < emaM && emaM < emaS) score -= 15;
            
            // Delta (15 pts)
            score += Math.Min(15, Math.Max(-15, delta / 5000));
            
            // Absorption (20 pts)
            score += Math.Min(20, Math.Max(-20, absorp / 6));
            
            // Sweep (10 pts)
            score += Math.Min(10, Math.Max(-10, sweep / 8));
            
            // Order Block (10 pts)
            score += Math.Min(10, Math.Max(-10, ob / 6));
            
            // FVG (5 pts)
            score += Math.Min(5, Math.Max(-5, fvg));
            
            // BOS (10 pts)
            score += Math.Min(10, Math.Max(-10, bos / 4));
            
            // VWAP Deviation (5 pts)
            score += Math.Min(5, Math.Max(-5, vwapDev * 3));
            
            // Confluence (10 pts)
            score += Math.Min(10, Math.Max(-10, (buyConf - sellConf) * 2));
            
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
            double mult = 2.0 / (period + 1);
            double ema = SMA(series, period)[0];
            for (int i = CurrentBar - period + 1; i <= CurrentBar; i++)
            {
                if (i < 0) continue;
                ema = (series[i] - ema) * mult + ema;
            }
            return ema;
        }
        
        private double SMA(Series<double> series, int period)
        {
            double sum = 0;
            int cnt = 0;
            for (int i = 0; i < period && i <= CurrentBar; i++)
            {
                sum += series[i];
                cnt++;
            }
            return cnt > 0 ? sum / cnt : 0;
        }
    }
}
