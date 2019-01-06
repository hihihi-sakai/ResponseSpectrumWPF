using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;

namespace WaveProcessWPF
{
    /// <summary>
    /// プロットする時間軸の種類
    /// </summary>
    public enum PlotTypeEnum
    {
        /// <summary>
        /// 周波数
        /// </summary>
        Frequency,
        /// <summary>
        /// 周期
        /// </summary>
        Second
    }

    public enum AxisTypeEnum
    {
        Liner,
        Logarithmic
    }

    public class RespSpectrum: INotifyPropertyChanged, INotifyDataErrorInfo
    {


        private IEnumerable<double> freq;
        private double minFreq;
        private double maxFreq;
        private int numFrequency;
        private PlotTypeEnum plotType;
        private AxisTypeEnum xAxisType;
        private ObservableCollection<double> hs;

        /// <summary>
        /// 減衰乗数リスト
        /// </summary>
        /// <value>
        /// The hs.
        /// </value>
        public ObservableCollection<double> Hs {
            get { return hs; }
            set
            {
                hs = value;
                RaisePropertyChanged();
            }
             }
        /// <summary>
        /// 出力する時間軸の種類
        /// </summary>
        /// <value>
        /// The type of the axis.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [XmlIgnore]
        public PlotTypeEnum PlotType
        {
            get { return plotType; }
            set
            {
                if (plotType == value) return;
                if (!Enum.IsDefined(typeof(PlotTypeEnum), value)) throw new ArgumentOutOfRangeException();
                plotType = value;
                calcFrequency();
                RaisePropertyChanged();
            }
        }

        [XmlIgnore]
        public AxisTypeEnum XAxisType
        {
            get { return xAxisType; }
            set
            {
                if (xAxisType == value) return;
                xAxisType = value;
                calcFrequency();
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// 出力を周期とするときの最小周期
        /// </summary>
        /// <value>
        /// The minimum sec.
        /// </value>
        [XmlIgnore]
        public double MinSec
        {
            get
            {
                return (maxFreq != 0) ? 1 / maxFreq : 0;
            }
            set
            {
                bool isErr = false;
                if (value <= 0)
                { 
                    AddError("MinSecは0を超える値としてください");
                    isErr = true;
                }
                double v = 1 / value;
                if (minFreq > v && minFreq > 0)
                {
                    AddError("MinSecはMaxSec以下の値としてください");
                    isErr = true;
                }
                if (isErr) return;
                if (maxFreq == v) return;
                maxFreq = v;
                calcFrequency();
                ResetError();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MaxFreq));
            }
        }
        /// <summary>
        /// 出力を周期とするときの最大周期
        /// </summary>
        /// <value>
        /// The maximum sec.
        /// </value>
        [XmlIgnore]
        public double MaxSec
        {
            get
            {
                return (minFreq != 0) ? 1 / minFreq : 0;
            }
            set
            {
                bool isErr = false;
                if (value <= 0) { AddError("MaxSecは0を超える値としてください"); isErr = true; }
                double v = 1 / value;
                if (v > maxFreq && MaxFreq > 0) { AddError("MaxSecはMinSec以上の値としてください"); isErr = true; }
                if (isErr) return;
                if (minFreq == v) return;
                minFreq = v;
                calcFrequency();
                ResetError();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MinFreq));
            }
        }
        /// <summary>
        /// 出力を周波数とするときの最終周波数
        /// </summary>
        /// <value>
        /// The minimum freq.
        /// </value>
        [XmlIgnore]
        public double MinFreq
        {
            get
            {
                return minFreq;
            }
            set
            {
                bool isErr = false;
                if (value <= 0) { AddError("MinFreqは0を超えるとしてください"); isErr = true; }
                if (value > maxFreq && MaxFreq > 0) { AddError("MinFreqはMaxFreq以下の値としてください"); isErr = true; }
                if (isErr) return;
                if (minFreq == value) return;
                minFreq = value;
                calcFrequency();
                ResetError();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MaxSec));
            }
        }
        /// <summary>
        /// 出力を周波数とするときの最大周波数
        /// </summary>
        /// <value>
        /// The maximum freq.
        /// </value>
        [XmlIgnore]
        public double MaxFreq
        {
            get
            {
                return maxFreq;
            }
            set
            {
                bool isErr = false;
                if (value <= 0) { AddError("MaxFreqは0を超える値としてください"); isErr = true; }
                if (minFreq > value && minFreq > 0) { AddError("MaxFreqはMinFreq以上の値としてください"); isErr = true; }
                if (isErr) return;
                if (maxFreq == value) return;
                maxFreq = value;
                calcFrequency();
                ResetError();
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(MinSec));
            }
        }
        /// <summary>
        /// 出力する周期/周波数の数
        /// </summary>
        /// <value>
        /// The number frequency.
        /// </value>
        [XmlIgnore]
        public int NumFrequency
        {
            get { return numFrequency; }
            set
            {
                bool isErr = false;
                if (value <= 1) { AddError("2以上の値としてください");isErr = true; }
                if (isErr) return;
                if (numFrequency == value) return;
                numFrequency = value;
                calcFrequency();
                ResetError();
                RaisePropertyChanged();
            }
        }

        public RespSpectrum()
        {
            Hs  = new ObservableCollection<double>();
            PlotType = PlotTypeEnum.Second;

        }

        private void calcFrequency()
        {
            switch (PlotType)
            {
                case PlotTypeEnum.Frequency:
                    switch (XAxisType)
                    {
                        case AxisTypeEnum.Liner:
                            freq = LinSpace(minFreq, maxFreq, NumFrequency);
                            break;
                        case AxisTypeEnum.Logarithmic:
                            freq = LogSpace(Math.Log10(minFreq), Math.Log10(maxFreq), NumFrequency);
                            break;
                        default:
                            break;
                    }
                    break;
                case PlotTypeEnum.Second:
                    switch (XAxisType)
                    {
                        case AxisTypeEnum.Liner:
                            freq = LinSpace(MinSec, MaxSec, NumFrequency).Select(v => 1d / v);
                            break;
                        case AxisTypeEnum.Logarithmic:
                            freq = LogSpace(Math.Log10(MinSec), Math.Log10(MaxSec), NumFrequency).Select(v => 1d / v);
                            break;
                        default:
                            break;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 等間隔な値を取得
        /// </summary>
        /// <param name="start">最小値</param>
        /// <param name="stop">最大値</param>
        /// <param name="num">取得する数</param>
        /// <param name="endpoint">最大値を含めるか<c>true</c> [endpoint].</param>
        /// <returns></returns>
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
                result = Enumerable.Range(0, num).Select(v => (v * step) + start).ToList();
            }
            else
            {
                var step = (stop - start) / (double)num;
                result = Enumerable.Range(0, num).Select(v => (v * step) + start).ToList();
            }

            return result;
        }

        /// <summary>
        /// 対数で等間隔な値を取得
        /// </summary>
        /// <param name="start">最小値.</param>
        /// <param name="stop">最大値</param>
        /// <param name="num">取得する数</param>
        /// <param name="endpoint">最大値を含めるか<c>true</c> [endpoint].</param>
        /// <param name="numericBase">対数の底</param>
        /// <returns></returns>
        private IEnumerable<double> LogSpace(double start, double stop, int num, bool endpoint = true, double numericBase = 10.0d)
        {
            var y = LinSpace(start, stop, num: num, endpoint: endpoint);
            //return power(y, numericBase);
            return y.Select(v => Math.Pow(numericBase, v));
        }


        /// <summary>
        /// 応答スペクトルを計算
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="progress">The progress.</param>
        /// <returns></returns>
        public IEnumerable<Spectra> Calculation(Wave input, IProgress<double> progress =null)
        {
            List<Spectra> anss = new List<Spectra>();
            double[] data = input.Data.ToArray();
            double[] f = freq.ToArray();
            long count = input.Data.LongCount();
            double[] ddx = new double[count];
            double[] dx = new double[count];
            double[] x = new double[count];

            for(int j=0; j<Hs.Count;j++)
            {
                double h = Hs[j];
                Spectra ans = new Spectra
                {
                    Name = input.Name,
                    H = h,
                    Dt=input.Dt
                };
                
                for(int i = 0; i < f.Length ;i++)
                {
                    //ニーガム法の解析パラメータの設定
                    double pi2 = 2 * Math.PI;
                    double w = pi2 * f[i];       // 固有振動数
                    double wd = w * Math.Sqrt(1.0 - Math.Pow(h, 2));
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

                    //初期値の設定
                    ddx[0] = 0d;
                    dx[0] = -data[0] * input.Dt;
                    x[0] = 0d;

                    //応答スペクトルの計算（1自由度系の地震応答）
                    //解析ループ       ニーガム法による解析
                    for (int m = 1; m < count; m++)
                    {
                        x[m] = a11 * x[m - 1] + a12 * dx[m - 1] + b11 * data[m - 1] + b12 * data[m];  // 応答の計算
                        dx[m] = a21 * x[m - 1] + a22 * dx[m - 1] + b21 * data[m - 1] + b22 * data[m]; // 応答の計算
                        ddx[m] = -2 * hw * dx[m] - w2 * x[m]; // 応答の計算
                    }

                    // 1自由度系の加速度，速度，変位の最大応答のセーブ（行方向；振動数，列方向；減衰）
                    ans.Data.Add(new Spectrum
                    {
                        Frequency = f[i],
                        Acceleration = ddx.Max((v) => Math.Abs(v)),
                        Velocity = dx.Max((v) => Math.Abs(v)),
                        Displacement = x.Max((v) => Math.Abs(v))
                    });

                    //progress?.Report((double)j / (double)Hs.Count + 1d / (double)Hs.Count * (double)i / (double)f.Length);

                }
                anss.Add(ans);
                progress?.Report((double)(j+1) / Hs.Count);

            }
            return anss;
        }


        #region INotifyPropertyChanged

        /// <summary>
        /// プロパティ値が変更するときに発生します。
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region INotifyDataErrorInfo
        readonly Dictionary<string, HashSet<string>> currentErrors = new Dictionary<string, HashSet<string>>();
        public bool HasErrors => currentErrors.Any();
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
        public IEnumerable GetErrors(string propertyName)
        {
            return GetErrorsG(propertyName);
        }
        public IEnumerable<string> GetErrorsG(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return currentErrors.Values.SelectMany(p => p).ToList().AsReadOnly();    //エンティティレベルでのエラー
            else if (currentErrors.ContainsKey(propertyName))
                return currentErrors[propertyName].ToList().AsReadOnly();
            else
                return Enumerable.Empty<string>();
        }
        protected bool AddError(string error, [CallerMemberName] string propertyName = "")
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException(nameof(propertyName));
            if (string.IsNullOrEmpty(error))
                throw new ArgumentException(nameof(error));
            if (!currentErrors.ContainsKey(propertyName))
                currentErrors[propertyName] = new HashSet<string>();
            bool ret = currentErrors[propertyName].Add(error);
            if (ret)
                RaiseErrorsChanged(propertyName);
            return ret;
        }
        protected bool ResetError([CallerMemberName]string propertyName = "")
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentException(nameof(propertyName));
            bool ret = currentErrors.Remove(propertyName);
            if (ret)
                RaiseErrorsChanged(propertyName);
            return ret;
        }
        private void RaiseErrorsChanged(string propertyName)
        {
            this.ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
        #endregion

    }
}
