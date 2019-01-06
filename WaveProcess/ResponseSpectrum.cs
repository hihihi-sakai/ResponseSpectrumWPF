using System;
using System.Collections.Generic;
using System.Linq;


namespace WaveProcess
{
    public class ResponseSpectrum
    {
        public enum AxisTypeEnum
        {
            FrequencyLiner,
            FrequencyLog,
            SecondLiner,
            SecondLog
        }

        private IEnumerable<double> freq;
        private double minFreq;
        private double maxFreq;


        public List<double> Hs { get; set; }
        public AxisTypeEnum AxisType { get; set; } = AxisTypeEnum.SecondLog;
        public double MinSec
        {
            get
            {
                return (minFreq != 0) ? 1 / minFreq : 0;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("MinSecは0以上の値としてください");
                double v = 1 / value;
                if (maxFreq == v) return;
                if (minFreq > v) throw new ArgumentOutOfRangeException("MinSecはMaxSec以下の値としてください");
                maxFreq = 1 / value;
                calcFrequency();
            }
        }
        public double MaxSec
        {
            get
            {
                return (maxFreq != 0) ? 1 / maxFreq : 0;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("MaxSecは0以上の値としてください");
                double v = 1 / value;
                if (minFreq == v) return;
                if (v > maxFreq) throw new ArgumentOutOfRangeException("MaxSecはMinSec以上の値としてください");
                minFreq = v;
                calcFrequency();
            }
        }
        public double MinFreq
        {
            get
            {
                return minFreq;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("MinFreqは0以上の値としてください");
                if (minFreq == value) return;
                if (value > maxFreq) throw new ArgumentOutOfRangeException("MinFreqはMaxFreq以下の値としてください");
                minFreq = value;
                calcFrequency();
            }
        }
        public double MaxFreq
        {
            get
            {
                return maxFreq;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException("MaxFreqは0以上の値としてください");
                if (maxFreq == value) return;
                if (minFreq > value) throw new ArgumentOutOfRangeException("MaxFreqはMinFreq以上の値としてください");
                maxFreq = value;
                calcFrequency();
            }
        }
        public int NumFrequency { get; set; }

        public ResponseSpectrum()
        {
            Hs  = new List<double>();

        }

        private void calcFrequency()
        {
            switch (AxisType)
            {
                case AxisTypeEnum.FrequencyLiner:
                    freq = LinSpace(minFreq, maxFreq, NumFrequency);
                    break;
                case AxisTypeEnum.FrequencyLog:
                    freq = LogSpace(Math.Log10(minFreq), Math.Log10(maxFreq), NumFrequency);
                    break;
                case AxisTypeEnum.SecondLiner:
                    freq = LinSpace(1.0 / maxFreq, 1.0 / minFreq, NumFrequency).Select(v => 1d / v);
                    break;
                case AxisTypeEnum.SecondLog:
                    freq = LogSpace(Math.Log10(1.0 / maxFreq), Math.Log10(1.0 / minFreq), NumFrequency).Select(v => 1d / v);
                    break;
                default:
                    break;
            }
        }

        private IEnumerable<double> arange(double start, int count)
        {
            return Enumerable.Range((int)start, count).Select(v => (double)v);
        }
        private IEnumerable<double> power(IEnumerable<double> exponents, double baseValue = 10.0d)
        {
            return exponents.Select(v => Math.Pow(baseValue, v));
        }

        private IEnumerable<double> LinSpace(double start, double stop, int num, bool endpoint = true)
        {
            var result = new List<double>();
            if (num <= 0)
            {
                return result;
            }

            if (endpoint)
            {
                if (num == 1)
                {
                    return new List<double>() { start };
                }

                var step = (stop - start) / ((double)num - 1.0d);
                result = arange(0, num).Select(v => (v * step) + start).ToList();
            }
            else
            {
                var step = (stop - start) / (double)num;
                result = arange(0, num).Select(v => (v * step) + start).ToList();
            }

            return result;
        }

        private IEnumerable<double> LogSpace(double start, double stop, int num, bool endpoint = true, double numericBase = 10.0d)
        {
            var y = LinSpace(start, stop, num: num, endpoint: endpoint);
            return power(y, numericBase);
        }

        public IEnumerable<Spectra> Calculation(Wave input)
        {
            List<Spectra> anss = new List<Spectra>();

            foreach (double h in Hs)
            {
                Spectra ans = new Spectra();
                ans.Name = input.Name;
                ans.h = h;
                anss.Add(ans);

                foreach (double f in freq)
                {
                    //ニーガム法の解析パラメータの設定
                    double pi2 = 2 * Math.PI;
                    double w = pi2 * f;       // 固有振動数
                    double wd = w * Math.Sqrt(1d - Math.Pow(h, 2));
                    double w2 = w * w;
                    double hw = h * w;
                    double wdt = wd * input.Dt;
                    double e = Math.Exp(-hw * input.Dt);
                    double coswdt = Math.Cos(wdt);
                    double sinwdt = Math.Sin(wdt);
                    double a11 = e * (coswdt + hw * sinwdt / wd);
                    double a12 = e * sinwdt / wd;
                    double a21 = -e * w2 * sinwdt / wd;
                    double a22 = e * (coswdt - hw * sinwdt / wd);
                    double ss = -hw * sinwdt - wd * coswdt;
                    double cc = -hw * coswdt + wd * sinwdt;
                    double s1 = (e * ss + wd) / w2;
                    double c1 = (e * cc + hw) / w2;
                    double s2 = (e * input.Dt * ss + hw * s1 + wd * c1) / w2;
                    double c2 = (e * input.Dt * cc + hw * c1 - wd * s1) / w2;
                    double s3 = input.Dt * s1 - s2;
                    double c3 = input.Dt * c1 - c2;
                    double b11 = -s2 / wdt;
                    double b12 = -s3 / wdt;
                    double b21 = (hw * s2 - wd * c2) / wdt;
                    double b22 = (hw * s3 - wd * c3) / wdt;

                    double[] ddx = new double[input.Data.LongCount()];
                    double[] dx = new double[input.Data.LongCount()];
                    double[] x = new double[input.Data.LongCount()];
                    //初期値の設定
                    ddx[0] = 0d;
                    dx[0] = -input.Data[0] * input.Dt;
                    x[0] = 0d;

                    //応答スペクトルの計算（1自由度系の地震応答）
                    //解析ループ       ニーガム法による解析
                    for (int m = 1; m < input.Data.LongCount(); m++)
                    {
                        x[m] = a11 * x[m - 1] + a12 * dx[m - 1] + b11 * input.Data[m - 1] + b12 * input.Data[m];  // 応答の計算
                        dx[m] = a21 * x[m - 1] + a22 * dx[m - 1] + b21 * input.Data[m - 1] + b22 * input.Data[m]; // 応答の計算
                        ddx[m] = -2 * hw * dx[m] - w2 * x[m]; // 応答の計算
                    }

                    // 1自由度系の加速度，速度，変位の最大応答のセーブ（行方向；振動数，列方向；減衰）
                    ans.Data.Add(new Spectrum() { Frequency = f, Acceleration = ddx.Max(), Velocity = dx.Max(), Displacement = x.Max() });
                }
            }
            return anss;
        }
    }


    public class Wave
    {
        public List<double> Data { get; set; } = new List<double>();
        public string Name { get; set; }
        public double Dt { get; set; }
    }
    
    public class Spectrum
    {
        public double Frequency { get; set; }
        public double Acceleration { get; set; }
        public double Velocity { get; set; }
        public double Displacement { get; set; }
    }

    public class Spectra
    {
        public string Name { get; set; }
        public double h { get; set; }
        public List<Spectrum> Data { get; set; } = new List<Spectrum>();
    }
}
