using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Wpf;
using System.IO;
using Microsoft.WindowsAPICodePack;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Dialogs.Controls;
using ClosedXML;
using ClosedXML.Excel;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;
using System.Runtime.Serialization.Json;

namespace WaveProcessWPF
{


    public class RespSpectrumViewModel: RespSpectrum
    {
        public enum UnitEnum
        {
            m_s2 = 0,
            gal =1,
            mm_s2=2
        }



        static readonly byte[,] DEFAULT_COLORS = new byte[,] {
            {0, 0, 255},
            {0, 0, 127},
            {255, 0, 0},
            {127, 0, 0},
            {0, 255, 0},
            {0, 127, 0},
            {0, 0, 0},
            {127, 127, 127},
            {255, 0, 255},
            {127, 0, 127},
            {0, 255, 255},
            {0, 127, 127},
            {255, 255, 0},
            {127, 127, 0},
            {0, 255, 127}
        };

        static readonly LineStyle[] DEFAULT_LINESTYLES = {
            LineStyle.Solid,
            LineStyle.Dot,
            LineStyle.Dash,
            LineStyle.DashDashDot,
            LineStyle.DashDashDotDot,
            LineStyle.LongDash,
            LineStyle.LongDashDot,
            LineStyle.LongDashDotDot,
        };


        //const double GRAV = 9.81;

        private double samplingPeriod;
        private UnitEnum unit;
        private string outputFolder;
        private string hInput ="";
        private bool isUpdateShownSpectrum =false;
        private double progressRate =0d;
        private string statusText;
        private Wave selectedWave;
        private object lockResults = new object();
        private Dictionary<string, OxyColor> plotColors = new Dictionary<string, OxyColor>();
        private Dictionary<double, LineStyle> plotLineStyles = new Dictionary<double, LineStyle>();
        private AxisTypeEnum yAxisType = AxisTypeEnum.Logarithmic;


        public ObservableCollection<Wave> Waves { get; set; }
        public ObservableCollection<SpectraViewModel> Results { get; private set; }

        [XmlIgnore]
        public Wave SelectedWave
        { get { return selectedWave; }
            private set
            {
                //if (selectedWave == value) return;
                selectedWave = value;
                DrawWave(selectedWave);
                selectedWave.PropertyChanged += (object sender, PropertyChangedEventArgs e) => {
                    if(e.PropertyName==nameof(Wave.Dt))DrawWave(selectedWave);
                    if (e.PropertyName == nameof(Wave.Name)) DrawWave(selectedWave);
                };
                RaisePropertyChanged();
            }
        }
        [XmlIgnore]
        public PlotModel WavePlotModel { get; private set; }
        [XmlIgnore]
        public PlotModel AccPlotModel { get; private set; }
        [XmlIgnore]
        public PlotModel VelPlotModel { get; private set; }
        [XmlIgnore]
        public PlotModel DispPlotModel { get; private set; }


        /// <summary>
        /// デフォルトのサンプリング周期
        /// </summary>
        /// <value>
        /// The sampling period.
        /// </value>
        [XmlIgnore]
        public double SamplingPeriod
        {
            get { return samplingPeriod; }
            set
            {
                bool isErr = false;
                if (samplingPeriod == value) return;
                if (value <= 0) { AddError($"{nameof(SamplingPeriod)}は0を超える値としてください");isErr = true;}
                if (!isErr) ResetError();
                samplingPeriod = value;
                RaisePropertyChanged();
            }
        }


        /// <summary>
        /// デフォルトの加速度単位
        /// </summary>
        /// <value>
        /// The unit.
        /// </value>
        [XmlIgnore]
        public UnitEnum Unit
        {
            get { return unit; }
            set
            {
                if (unit == value) return;
                unit = value;
                RaisePropertyChanged();
            }
        }

        [XmlIgnore]
        public string HInput
        {
            get { return hInput; }
            set
            {
                bool isErr = false;
                double d;
                if(value =="")
                {
                    hInput = value;
                    ResetError();
                    RaisePropertyChanged();
                    AddDampingCommand.RaiseCanExecuteChanged();
                    return;
                }
                if(!double.TryParse(value, out d)) { AddError($"小数としてください"); isErr = true; }
                if (d < 0 || d >= 1d) { AddError($"減衰は0以上、1未満の値としてください"); isErr = true; }
                if (value == hInput) return;
                hInput = value;
                if (!isErr)
                {
                    ResetError();
                    RaisePropertyChanged();
                }
                AddDampingCommand.RaiseCanExecuteChanged();
            }
        }

        [XmlIgnore]
        public string OutputFolder
        {
            get { return outputFolder; }
            set
            {
                if (outputFolder == value) return;
                if (!System.IO.Directory.Exists(value))
                {
                    //throw new System.IO.DirectoryNotFoundException();
                    AddError("アウトプットフォルダが存在しません");
                    return;
                }
                outputFolder = value;
                ResetError();
                RaisePropertyChanged();
            }
        }

        [XmlIgnore]
        public double ProgressRate
        {
            get { return progressRate; }
            set
            {
                progressRate = value;
                RaisePropertyChanged();
            }
        }

        [XmlIgnore]
        public string StatusText
        {
            get { return statusText; }
            set { statusText = value; RaisePropertyChanged(); }
        }


        [XmlIgnore]
        public Dictionary<string, OxyColor> PlotColors
        {
            get { return plotColors; }
            set { plotColors = value; }
        }


        [XmlIgnore]
        public Dictionary<double, LineStyle>  PlotLineStyles
        {
            get { return plotLineStyles; }
            set { plotLineStyles = value; }
        }

        
        public AxisTypeEnum YAxisType
        {
            get { return yAxisType; }
            set
            {
                if (yAxisType == value) return;
                yAxisType = value;
                RaisePropertyChanged();

                if (AccPlotModel != null)
                {
                    SetGraph(AccPlotModel, XAxisType, PlotType, YAxisType);
                    AccPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "加速度[m/s2]";
                    AccPlotModel.InvalidatePlot(true);
                }
                if (VelPlotModel != null)
                {
                    SetGraph(VelPlotModel, XAxisType, PlotType, YAxisType);
                    VelPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "速度[m/s]";
                    VelPlotModel.InvalidatePlot(true);
                }
                if (DispPlotModel != null)
                {
                    SetGraph(DispPlotModel, XAxisType, PlotType, YAxisType);
                    DispPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "変位[m]";
                    DispPlotModel.InvalidatePlot(true);
                }
            }
        }


        [XmlIgnore]
        public Visibility IsVisibleFrequency {
            get
            {
                return (base.PlotType== PlotTypeEnum.Frequency)? Visibility.Visible: Visibility.Collapsed;
            }
        }
        [XmlIgnore]
        public Visibility IsVisiblePeriod
        {
            get
            {
                return (base.PlotType == PlotTypeEnum.Second) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public RespSpectrumViewModel():base()
        {
            

            Waves = new ObservableCollection<Wave>();
            Results = new ObservableCollection<SpectraViewModel>();
            System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Results, lockResults);

            Results.CollectionChanged += Results_CollectionChanged;
            PropertyChanged += RespSpec_PropertyChanged;

            //プロパティ初期化
            SamplingPeriod = Properties.Settings.Default.Dt;
            Enum.TryParse<UnitEnum>(Properties.Settings.Default.Unit,out unit);
            string p = Properties.Settings.Default.OutputFolder;
            OutputFolder=(Directory.Exists(p))? p: System.IO.Directory.GetCurrentDirectory();
            try
            {
                p = Properties.Settings.Default.DampingCoefficient;
                double[] hs = p.Split(',').Select((s) => double.Parse(s)).ToArray();
                foreach (double d in hs) base.Hs.Add(d);
            }
            catch
            {
                //エラーや空欄の時
                Hs.Add(0.03);
                Hs.Add(0.05);
                Hs.Add(0.10);
                Hs.Add(0.15);
                Hs.Add(0.20);
            }
            MaxSec = Properties.Settings.Default.MaxSec;
            MinSec = Properties.Settings.Default.MinSec;
            NumFrequency = Properties.Settings.Default.NumFrequency;
            PlotTypeEnum ptype = PlotTypeEnum.Second;
            Enum.TryParse<PlotTypeEnum>(Properties.Settings.Default.PlotType, out ptype);
            PlotType = ptype;
            AxisTypeEnum atype = AxisTypeEnum.Logarithmic;
            Enum.TryParse<AxisTypeEnum>(Properties.Settings.Default.XAxisType, out atype);
            XAxisType = atype;
            atype = AxisTypeEnum.Logarithmic;
            Enum.TryParse<AxisTypeEnum>(Properties.Settings.Default.YAxisType, out atype);
            YAxisType = atype;


        }

        public void Closing()
        {
            Properties.Settings.Default.Dt = SamplingPeriod;
            Properties.Settings.Default.Unit = Enum.GetName(typeof(UnitEnum), Unit);
            Properties.Settings.Default.OutputFolder = OutputFolder;
            Properties.Settings.Default.DampingCoefficient = string.Join(",", Hs);
            Properties.Settings.Default.MaxSec = MaxSec;
            Properties.Settings.Default.MinSec = MinSec;
            Properties.Settings.Default.NumFrequency = NumFrequency;
            Properties.Settings.Default.PlotType = Enum.GetName(typeof(PlotTypeEnum), PlotType);
            Properties.Settings.Default.XAxisType = Enum.GetName(typeof(AxisTypeEnum), XAxisType);
            Properties.Settings.Default.YAxisType = Enum.GetName(typeof(AxisTypeEnum), YAxisType);
            Properties.Settings.Default.Save();
        }

        private void Results_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach(SpectraViewModel r in e.NewItems)
                    {
                        r.PropertyChanged += (object s, PropertyChangedEventArgs args) => { if (args.PropertyName == nameof(SpectraViewModel.IsShow)) DrawGraph(); };
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                        break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    break;
                default:
                    break;
            }
        }

        private void RespSpec_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PlotType):
                    RaisePropertyChanged(nameof(IsVisibleFrequency));
                    RaisePropertyChanged(nameof(IsVisiblePeriod));
                    if (AccPlotModel != null)
                    {
                        SetGraph(AccPlotModel, XAxisType, PlotType, YAxisType);
                        AccPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "加速度[m/s2]";
                        AccPlotModel.InvalidatePlot(true);
                    }
                    if (VelPlotModel != null)
                    {
                        SetGraph(VelPlotModel, XAxisType, PlotType, YAxisType);
                        VelPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "速度[m/s]";
                        VelPlotModel.InvalidatePlot(true);
                    }
                    if (DispPlotModel != null)
                    {
                        SetGraph(DispPlotModel, XAxisType, PlotType, YAxisType);
                        DispPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "変位[m]";
                        DispPlotModel.InvalidatePlot(true);
                    }
                    DrawGraph();
                    break;
                case nameof(XAxisType):
                    if (AccPlotModel != null)
                    {
                        SetGraph(AccPlotModel, XAxisType, PlotType, YAxisType);
                        AccPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "加速度[m/s2]";
                        AccPlotModel.InvalidatePlot(true);
                    }
                    if (VelPlotModel != null)
                    {
                        SetGraph(VelPlotModel, XAxisType, PlotType, YAxisType);
                        VelPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "速度[m/s]";
                        VelPlotModel.InvalidatePlot(true);
                    }
                    if (DispPlotModel != null)
                    {
                        SetGraph(DispPlotModel, XAxisType, PlotType, YAxisType);
                        DispPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "変位[m]";
                        DispPlotModel.InvalidatePlot(true);
                    }
                    break;
                default:
                    break;
            }
            
        }

        /// <summary>
        /// グラフのデータ以外のパラメータを設定
        /// </summary>
        /// <param name="m">The m.</param>
        private void SetGraph(PlotModel m, AxisTypeEnum xAxisType, PlotTypeEnum xPlotType, AxisTypeEnum yAxisType)
        {
            m.Axes.Clear();
            OxyPlot.Axes.Axis xAxis=null;
            switch (xAxisType)
            {
                case AxisTypeEnum.Liner:
                    xAxis = new OxyPlot.Axes.LinearAxis();
                    break;
                case AxisTypeEnum.Logarithmic:
                    xAxis = new OxyPlot.Axes.LogarithmicAxis();
                    break;
                default:
                    throw new ArgumentException("SetGraph: xAxisTypeが範囲外です");
            }
            switch (xPlotType)
            {
                case PlotTypeEnum.Frequency:
                    xAxis.Title = "周波数[Hz]";
                    xAxis.Key = "Frequency";
                    break;
                case PlotTypeEnum.Second:
                    xAxis.Title = "周期[s]";
                    xAxis.Key = "Second";
                    break;
                default:
                    throw new ArgumentException("SetGraph: xPlotTypeが範囲外です");
            }
            xAxis.Position = AxisPosition.Bottom;
            xAxis.MajorGridlineStyle = LineStyle.Automatic;
            xAxis.MinorGridlineStyle = LineStyle.Automatic;
            m.Axes.Add(xAxis);

            OxyPlot.Axes.Axis yAxis =null;
            switch (yAxisType)
            {
                case AxisTypeEnum.Liner:
                    yAxis = new OxyPlot.Axes.LinearAxis();
                    break;
                case AxisTypeEnum.Logarithmic:
                    yAxis = new OxyPlot.Axes.LogarithmicAxis();
                    break;
                default:
                    throw new ArgumentException("SetGraph: yAxisTypeが範囲外です");
            }
            yAxis.Position = AxisPosition.Left;
            yAxis.MajorGridlineStyle = LineStyle.Automatic;
            yAxis.MinorGridlineStyle = LineStyle.Automatic;
            m.Axes.Add(yAxis);


        }

        private void InvalidateGraph()
        {
            if (isUpdateShownSpectrum) return;
            isUpdateShownSpectrum = true;
            RedrawGraphCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Plotオブジェトに波形を描画
        /// </summary>
        /// <param name="w">The w.</param>
        private void DrawWave(Wave w)
        {
            WavePlotModel.Series.Clear();
            w.CalculateMax();
            OxyPlot.Series.LineSeries wavePoints = new OxyPlot.Series.LineSeries()
            {
                Title = $"{w.Name}, Dt={w.Dt}s",
                StrokeThickness = 0.5,
            };
            var dat = w.Data.ToArray();
            for (long i = 0; i < dat.Count(); i++)
            {
                wavePoints.Points.Add(new DataPoint((double)i * w.Dt, dat[i]));
            }
            WavePlotModel.Series.Add(wavePoints);
            WavePlotModel.InvalidatePlot(true);
        }

        /// <summary>
        /// Plotオブジェトに応答スペクトルを描画
        /// </summary>
        private void DrawGraph()
        {
            if (AccPlotModel == null) return;
            AccPlotModel.Series.Clear();
            VelPlotModel.Series.Clear();
            DispPlotModel.Series.Clear();

            foreach (var s in Results.Where(r => r.IsShow))
            {
                OxyPlot.Series.LineSeries accPoints = new OxyPlot.Series.LineSeries()
                {
                    Title = $"{s.Name} h={s.H.ToString()}",
                    StrokeThickness = 1.5,
                    Color = (PlotColors.ContainsKey(s.Name))?PlotColors[s.Name]:OxyColors.Automatic,
                    LineStyle=(PlotLineStyles.ContainsKey(s.H))?PlotLineStyles[s.H]:LineStyle.Automatic,
                };
                    
                OxyPlot.Series.LineSeries velPoints = new OxyPlot.Series.LineSeries()
                {
                    Title = $"{s.Name} h={s.H.ToString()}",
                    StrokeThickness = 1.5,
                    Color = (PlotColors.ContainsKey(s.Name)) ? PlotColors[s.Name] : OxyColors.Automatic,
                    LineStyle = (PlotLineStyles.ContainsKey(s.H)) ? PlotLineStyles[s.H] : LineStyle.Automatic,
                };
                OxyPlot.Series.LineSeries dispPoints = new OxyPlot.Series.LineSeries()
                {
                    Title = $"{s.Name} h={s.H.ToString()}",
                    StrokeThickness = 1.5,
                    Color = (PlotColors.ContainsKey(s.Name)) ? PlotColors[s.Name] : OxyColors.Automatic,
                    LineStyle = (PlotLineStyles.ContainsKey(s.H)) ? PlotLineStyles[s.H] : LineStyle.Automatic,
                };
                var dat = s.Data.ToArray();
                switch (PlotType)
                {
                    case PlotTypeEnum.Frequency:
                        for (long i = 0; i < dat.Count(); i++)
                        {
                            accPoints.Points.Add(new DataPoint(dat[i].Frequency, dat[i].Acceleration));
                            velPoints.Points.Add(new DataPoint(dat[i].Frequency, dat[i].Velocity));
                            dispPoints.Points.Add(new DataPoint(dat[i].Frequency, dat[i].Displacement));
                        }

                        break;
                    case PlotTypeEnum.Second:
                        for (long i = 0; i < dat.Count(); i++)
                        {
                            accPoints.Points.Add(new DataPoint( dat[i].Second, dat[i].Acceleration));
                            velPoints.Points.Add(new DataPoint( dat[i].Second, dat[i].Velocity));
                            dispPoints.Points.Add(new DataPoint( dat[i].Second, dat[i].Displacement));
                        }
                        break;
                    default:
                        break;
                }

                AccPlotModel.Series.Add(accPoints);
                VelPlotModel.Series.Add(velPoints);
                DispPlotModel.Series.Add(dispPoints);
                }


            AccPlotModel.InvalidatePlot(true);
            VelPlotModel.InvalidatePlot(true);
            DispPlotModel.InvalidatePlot(true);

            isUpdateShownSpectrum = false;
            RedrawGraphCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// フォルダが削除可能か確認
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        ///   <c>true</c> if [is read only] [the specified path]; otherwise, <c>false</c>.
        /// </returns>
        static private bool IsReadOnly(string path)
        {
            try
            {
                var attr = File.GetAttributes(path);
                return ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            }
            catch(FileNotFoundException )
            {
                return true;
            }
            catch(DirectoryNotFoundException )
            {
                return true;

            }
        }

        /// <summary>
        /// Extracts the resource.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <param name="resouceName">Name of the resouce.</param>
        /// <exception cref="Exception">リソースを取得できませんでした</exception>
        static private void ExtractResource(string folder, string resouceName)
        {
            Uri uri = new Uri(resouceName, UriKind.Relative);
            if (uri == null) throw new Exception("リソースを取得できませんでした");
            System.Windows.Resources.StreamResourceInfo info = Application.GetResourceStream(uri);
            string filename = Path.GetFileName(resouceName);
            FileStream fs = new FileStream($"{folder}\\{filename}", FileMode.OpenOrCreate, FileAccess.Write);
            info.Stream.CopyTo(fs);
            info.Stream.Close();
            fs.Flush();
            fs.Close();
        }

        /// <summary>
        /// Outputs the result as text.
        /// </summary>
        private void OutputResultAsText()
        {
            if (!Results.Any())
            {
                Debug.WriteLine("結果がありませんでした");
                StatusText = "結果がありませんでした";
                return;
            }

            if (!Directory.Exists(OutputFolder))
            {
                Debug.WriteLine("出力フォルダが存在しません");
                StatusText = "出力フォルダが存在しません";
                return;
            }

            var attr = File.GetAttributes(OutputFolder);
            bool b = ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            if (b)
            {
                Debug.WriteLine("フォルダに書き込めません");
                StatusText = "フォルダに書き込めません";
                return;
            }

            foreach (var r in Results.GroupBy(a => a.Name))
            {
                StreamWriter swacc = null;
                StreamWriter swvel = null;
                StreamWriter swdisp = null;
                try
                {
                    swacc = new StreamWriter(OutputFolder + "\\RespAcc_" + r.Key + ".dat");
                    swvel = new StreamWriter(OutputFolder + "\\RespVel_" + r.Key + ".dat");
                    swdisp = new StreamWriter(OutputFolder + "\\RespDis_" + r.Key + ".dat");
                    //ヘッダ
                    string headname = $"%=Freq.[Hz]== ==Period[s]==";
                    var hs = r.OrderBy(a => a.H).Select(a => a.H).ToArray();
                    for (int j = 0; j < hs.Count(); j++) //列
                    {
                        headname += string.Format(" ==h:{0,7:f}==", hs[j]);
                    }
                    swacc.WriteLine("%" + new string('=', 79));
                    swacc.WriteLine("%      応答スペクトル(加速度)");
                    swacc.WriteLine($"%      Data name = {r.Key}, dt= {r.First().Dt}, Data count= {r.First().Data.LongCount()}");
                    swacc.WriteLine("%" + new string('=', 79));
                    swacc.WriteLine(headname);
                    swvel.WriteLine("%" + new string('=', 79));
                    swvel.WriteLine("%      応答スペクトル(速度)");
                    swvel.WriteLine($"%      Data name = {r.Key}, dt= {r.First().Dt}, Data count= {r.First().Data.LongCount()}");
                    swvel.WriteLine("%" + new string('=', 79));
                    swvel.WriteLine(headname);
                    swdisp.WriteLine("%" + new string('=', 79));
                    swdisp.WriteLine("%      応答スペクトル(変位)");
                    swdisp.WriteLine($"%      Data name = {r.Key}, dt= {r.First().Dt}, Data count= {r.First().Data.LongCount()}");
                    swdisp.WriteLine("%" + new string('=', 79));
                    swdisp.WriteLine(headname);

                    for (int i = 0; i < r.First().Data.Count(); i++) //行
                    {
                        swacc.Write($"{r.First().Data[i].Frequency,12:E} {1 / r.First().Data[i].Frequency,12:E}");
                        swvel.Write($"{r.First().Data[i].Frequency,12:E} {1 / r.First().Data[i].Frequency,12:E}");
                        swdisp.Write($"{r.First().Data[i].Frequency,12:E} {1 / r.First().Data[i].Frequency,12:E}");
                        for (int j = 0; j < hs.Count(); j++) //列
                        {
                            swacc.Write($" {r.First(a => a.H == hs[j]).Data[i].Acceleration:E}");
                            swvel.Write($" {r.First(a => a.H == hs[j]).Data[i].Velocity:E}");
                            swdisp.Write($" {r.First(a => a.H == hs[j]).Data[i].Displacement:E}");
                        }
                        swacc.WriteLine();
                        swvel.WriteLine();
                        swdisp.WriteLine();
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show($"書き込めないファイルがありました: {r.Key}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    continue;
                }
                finally
                {
                    swacc?.Close();
                    swvel?.Close();
                    swdisp?.Close();
                }
            }
        }

        private void OutputResultAsCsv()
        {
            if (!Results.Any())
            {
                Debug.WriteLine("結果がありませんでした");
                StatusText = "結果がありませんでした";
                return;
            }

            if (!Directory.Exists(OutputFolder))
            {
                Debug.WriteLine("出力フォルダが存在しません");
                StatusText = "出力フォルダが存在しません";
                return;
            }

            var attr = File.GetAttributes(OutputFolder);
            bool b = ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly);
            if (b)
            {
                Debug.WriteLine("フォルダに書き込めません");
                StatusText = "フォルダに書き込めません";
                return;
            }

            StreamWriter swacc = null;
            StreamWriter swvel = null;
            StreamWriter swdisp = null;
            string[,] stracc = new string[Results[0].Data.Count +2,Results.Count +2];
            string[,] strvel = new string[Results[0].Data.Count + 2, Results.Count + 2];
            string[,] strdisp = new string[Results[0].Data.Count + 2, Results.Count + 2];

            try
            {
                int col = 2;
                int row = 2;

                stracc[0, 0] = "acceleration response spectrum";
                strvel[0, 0] = "velocity response spectrum";
                strdisp[0, 0] = "displacement response spectrum";
                stracc[0, 1] = "";
                strvel[0, 1] = "";
                strdisp[0, 1] = "";
                stracc[1, 0] = "Frequency[Hz]";
                strvel[1, 0] = "Frequency[Hz]";
                strdisp[1, 0] = "Frequency[Hz]";
                stracc[1, 1] = "Period[s]";
                strvel[1, 1] = "Period[s]";
                strdisp[1, 1] = "Period[s]";
                foreach (var c in Results[0].Data)
                {
                    stracc[row, 0] = c.Frequency.ToString();
                    strvel[row, 0] = c.Frequency.ToString();
                    strdisp[row, 0] = c.Frequency.ToString();
                    stracc[row, 1] = c.Second.ToString();
                    strvel[row, 1] = c.Second.ToString();
                    strdisp[row, 1] = c.Second.ToString();
                    row++;
                }
                foreach (var r in Results.GroupBy(a => a.Name))
                    foreach (var s in r.OrderBy(t=>t.H))
                    {
                        stracc[0, col] = s.Name.Replace(',','_');
                        strvel[0, col] = s.Name.Replace(',', '_');
                        strdisp[0, col] = s.Name.Replace(',', '_');
                        stracc[1, col] = $"h={s.H.ToString()}";
                        strvel[1, col] = $"h={s.H.ToString()}";
                        strdisp[1, col] = $"h={s.H.ToString()}";
                        for (row = 0; row<s.Data.Count;row++)
                        {
                            stracc[row+2, col] = s.Data[row].Acceleration.ToString();
                            strvel[row+2, col] = s.Data[row].Velocity.ToString();
                            strdisp[row+2, col] = s.Data[row].Displacement.ToString();
                        }
                        col++;
                    }

                swacc = new StreamWriter(OutputFolder + "\\RespAcc.csv");
                swvel = new StreamWriter(OutputFolder + "\\RespVel.csv");
                swdisp = new StreamWriter(OutputFolder + "\\RespDis.csv");

                for (row = 0; row < Results[0].Data.Count + 2; row++) //行
                {
                    for(col=0;col<Results.Count+2;col++)
                    {
                        swacc.Write($"{stracc[row,col]},");
                        swvel.Write($"{strvel[row, col]},");
                        swdisp.Write($"{strdisp[row, col]},");
                    }
                    swacc.WriteLine();
                    swvel.WriteLine();
                    swdisp.WriteLine();
                }
            }
            catch (Exception)
            {
                MessageBox.Show($"書き込めないファイルがありました", "エラー", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                
            }
            finally
            {
                swacc?.Close();
                swvel?.Close();
                swdisp?.Close();
            }


        }

        private static void OutputResultAsHtml(RespSpectrumViewModel model, string filename = "RespSpec.html", string folderName = "Report")
        {
            //const string folderName = "Report";
            int i;
            if( Directory.Exists($"{model.OutputFolder}\\{folderName}"))
                try
                {
                    Func<DirectoryInfo, DirectoryInfo> func =null;
                    func= (DirectoryInfo dirInfo) => {
                        //基のフォルダの属性を変更
                        if ((dirInfo.Attributes & FileAttributes.ReadOnly) ==
                            FileAttributes.ReadOnly)
                            dirInfo.Attributes = FileAttributes.Normal;
                        //フォルダ内のすべてのファイルの属性を変更
                        foreach (FileInfo fi in dirInfo.GetFiles())
                            if ((fi.Attributes & FileAttributes.ReadOnly) ==
                                FileAttributes.ReadOnly)
                                fi.Attributes = FileAttributes.Normal;
                        //サブフォルダの属性を回帰的に変更
                        foreach (DirectoryInfo di in dirInfo.GetDirectories())
                        {
                            func(di);
                            di.Delete();
                        }
                        return dirInfo;
                    };

                    DirectoryInfo dir = new DirectoryInfo($"{model.OutputFolder}\\{folderName}");
                    func(dir);
                    //Directory.Delete($"{model.OutputFolder}\\{folderName}",true);
                }
                catch (Exception)
                {
                    MessageBox.Show($"古いフォルダを消せませんでした:\n{model.OutputFolder}\\{folderName}");
                    return;
                }
            else
                Directory.CreateDirectory($"{model.OutputFolder}\\{folderName}");
           
            //XMLでデータを保存
            XmlSerializer serializer = new XmlSerializer(typeof(RespSpectrumViewModel));
            MemoryStream xmlMemStream = new MemoryStream();
            StreamWriter xmlSw = new StreamWriter(xmlMemStream, new UTF8Encoding(false));
            serializer.Serialize(xmlSw, model);
            xmlSw.Flush();
            xmlMemStream.Seek(0, SeekOrigin.Begin);
            XmlReaderSettings settings = new XmlReaderSettings();
            XmlReader xslsr = XmlReader.Create(xmlMemStream);


            //XMLをファイルで保存(デバッグ)
            //string xmlfilename = $"{model.OutputFolder}\\{folderName}\\RespSpec.xml";
            //StreamWriter xmlSw2 = new StreamWriter(xmlfilename,false, new UTF8Encoding(false));
            //serializer.Serialize(xmlSw2, model);
            //xmlSw2.Close();

            //JSONをファイルで保存
            //string jsonfilename = $"{model.OutputFolder}\\{folderName}\\RespSpec.json";
            //FileStream jsonSw = new FileStream(jsonfilename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
            //DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(RespSpectrumViewModel));
            //jsonSerializer.WriteObject(jsonSw, model);
            //jsonSw.Close();

            Uri uri = new Uri("/RespSpectrumViewModel.xslt", UriKind.Relative);
            System.Windows.Resources.StreamResourceInfo info = Application.GetResourceStream(uri);
            XmlReader xmlReader = XmlReader.Create(info.Stream);
            XslCompiledTransform xslTrans = new XslCompiledTransform();
            xslTrans.Load(xmlReader);
            XsltArgumentList param = new XsltArgumentList();

            string outputFileName = $"{model.OutputFolder}\\{folderName}\\{filename}";
            StreamWriter outSw = new StreamWriter(outputFileName,false, new UTF8Encoding(false));
            xslTrans.Transform(xslsr, param, outSw);
            //xslTrans.Transform(xmlfilename, outputFileName);

            ExtractResource($"{model.OutputFolder}\\{folderName}", "/Content/bootstrap.min.css");
            ExtractResource($"{model.OutputFolder}\\{folderName}", "/Scripts/jquery-3.3.1.slim.min.js");
            ExtractResource($"{model.OutputFolder}\\{folderName}", "/Scripts/bootstrap.bundle.min.js");

            outSw.Close();
            xmlSw.Close();
            xmlMemStream.Close();

            //波形の出力
            foreach (var item in model.Waves)
            {
                PlotModel WavePlotModel = new PlotModel() { Title = $"{item.Name}" };
                //軸設定
                WavePlotModel.Axes.Clear();
                OxyPlot.Axes.LinearAxis xLinearAxis = new OxyPlot.Axes.LinearAxis();
                xLinearAxis.Position = AxisPosition.Bottom;
                xLinearAxis.Title = "時間[s]";
                xLinearAxis.Key = "Time";
                xLinearAxis.MajorGridlineStyle = LineStyle.Automatic;
                xLinearAxis.MinorGridlineStyle = LineStyle.Automatic;
                WavePlotModel.Axes.Add(xLinearAxis);
                OxyPlot.Axes.LinearAxis yAxis = new OxyPlot.Axes.LinearAxis();
                yAxis.Position = AxisPosition.Left;
                yAxis.Title = "加速度[m/s2]";
                xLinearAxis.Key = "Acc";
                yAxis.MajorGridlineStyle = LineStyle.Automatic;
                yAxis.MinorGridlineStyle = LineStyle.Automatic;
                WavePlotModel.Axes.Add(yAxis);

                OxyPlot.Series.LineSeries wavePoints = new OxyPlot.Series.LineSeries()
                {
                    Title = $"{item.Name}, Dt={item.Dt}s",
                    StrokeThickness = 0.5,
                };
                var dat = item.Data.ToArray();
                for (long j = 0; j < dat.Count(); j++)
                {
                    wavePoints.Points.Add(new DataPoint((double)j * item.Dt, dat[j]));
                }
                WavePlotModel.Series.Add(wavePoints);
                WavePlotModel.InvalidatePlot(true);

                PngExporter pngExporter = new PngExporter { Width = 950, Height = 400, Background = OxyColors.White };
                pngExporter.ExportToFile(WavePlotModel, $"{model.OutputFolder}\\{folderName}\\WAVE_{item.Name}.png");
            }


            //応答スペクトル
            PlotModel AccPlotModel = new PlotModel();
            PlotModel VelPlotModel = new PlotModel();
            PlotModel DispPlotModel = new PlotModel();
            model.SetGraph(AccPlotModel, model.XAxisType, model.PlotType, model.YAxisType);
            model.SetGraph(VelPlotModel, model.XAxisType, model.PlotType, model.YAxisType);
            model.SetGraph(DispPlotModel, model.XAxisType, model.PlotType, model.YAxisType);
            AccPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "加速度[m/s2]";
            VelPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "速度[m/s]";
            DispPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "変位[m]";
            foreach (var item in model.Waves)
            {
                AccPlotModel.Title = $"加速度応答スペクトル: {item.Name}";
                VelPlotModel.Title = $"速度応答スペクトル: {item.Name}";
                DispPlotModel.Title = $"変位応答スペクトル: {item.Name}";
                AccPlotModel.Series.Clear();
                VelPlotModel.Series.Clear();
                DispPlotModel.Series.Clear();
                AccPlotModel.LegendPosition = model.AccPlotModel.LegendPosition;
                VelPlotModel.LegendPosition = model.VelPlotModel.LegendPosition;
                DispPlotModel.LegendPosition = model.DispPlotModel.LegendPosition;
                foreach (var s in model.Results.Where(r=>r.Name==item.Name))
                {
                    OxyPlot.Series.LineSeries accPoints = new OxyPlot.Series.LineSeries()
                    {
                        Title = $"h={s.H.ToString()}",
                        StrokeThickness = 1.5,
                        Color = (model.PlotColors.ContainsKey(s.Name)) ? model.PlotColors[s.Name] : OxyColors.Automatic,
                        LineStyle = (model.PlotLineStyles.ContainsKey(s.H)) ? model.PlotLineStyles[s.H] : LineStyle.Automatic,

                    };
                    OxyPlot.Series.LineSeries velPoints = new OxyPlot.Series.LineSeries()
                    {
                        Title = $"h={s.H.ToString()}",
                        StrokeThickness = 1.5,
                        Color = (model.PlotColors.ContainsKey(s.Name)) ? model.PlotColors[s.Name] : OxyColors.Automatic,
                        LineStyle = (model.PlotLineStyles.ContainsKey(s.H)) ? model.PlotLineStyles[s.H] : LineStyle.Automatic,
                    };
                    OxyPlot.Series.LineSeries dispPoints = new OxyPlot.Series.LineSeries()
                    {
                        Title = $"h={s.H.ToString()}",
                        StrokeThickness = 1.5,
                        Color = (model.PlotColors.ContainsKey(s.Name)) ? model.PlotColors[s.Name] : OxyColors.Automatic,
                        LineStyle = (model.PlotLineStyles.ContainsKey(s.H)) ? model.PlotLineStyles[s.H] : LineStyle.Automatic,
                    };
                    var dat = s.Data.ToArray();
                    switch (model.PlotType)
                    {
                        case PlotTypeEnum.Frequency:
                            for (i = 0; i < dat.Count(); i++)
                            {
                                accPoints.Points.Add(new DataPoint(dat[i].Frequency, dat[i].Acceleration));
                                velPoints.Points.Add(new DataPoint(dat[i].Frequency, dat[i].Velocity));
                                dispPoints.Points.Add(new DataPoint(dat[i].Frequency, dat[i].Displacement));
                            }
                            break;
                        case PlotTypeEnum.Second:
                            for (i = 0; i < dat.Count(); i++)
                            {
                                accPoints.Points.Add(new DataPoint(dat[i].Second, dat[i].Acceleration));
                                velPoints.Points.Add(new DataPoint(dat[i].Second, dat[i].Velocity));
                                dispPoints.Points.Add(new DataPoint(dat[i].Second, dat[i].Displacement));
                            }
                            break;
                        default:
                            break;
                    }

                    AccPlotModel.Series.Add(accPoints);
                    VelPlotModel.Series.Add(velPoints);
                    DispPlotModel.Series.Add(dispPoints);
                }

                AccPlotModel.InvalidatePlot(true);
                VelPlotModel.InvalidatePlot(true);
                DispPlotModel.InvalidatePlot(true);
                PngExporter pngExporter = new PngExporter { Width = 950, Height = 550, Background = OxyColors.White };
                pngExporter.ExportToFile(AccPlotModel, $"{model.OutputFolder}\\{folderName}\\RespAcc_{item.Name}.png");
                pngExporter.ExportToFile(VelPlotModel, $"{model.OutputFolder}\\{folderName}\\RespVel_{item.Name}.png");
                pngExporter.ExportToFile(DispPlotModel, $"{model.OutputFolder}\\{folderName}\\RespDis_{item.Name}.png");
            }

        }

        /// <summary>
        /// Outputs the result as excel.
        /// </summary>
        /// <param name="wb">The wb.</param>
        private void OutputResultAsExcel(XLWorkbook wb)
        {
            const int H_ROW = 7;
            const int NAME_COL = 3;
            const int ID_ROW = 1;
            const int TITLE_ROW = 2;
            const int FLEQ_COL = 1;
            const int SEQ_COL = 2;
            const int DATA_COL = 3;

            if (Results.Count == 0) return;

            IXLWorksheet mainWs = wb.Worksheet("操作");
            if (mainWs == null) throw new ApplicationException("操作シートがありませんでした");
            for (int c = 0; c < Hs.Count; c++)
            {
                mainWs.Cell(H_ROW, NAME_COL + 1 + c).Value = Hs[c];
            }
            for (int r = 0; r < Waves.Count; r++)
            {
                mainWs.Cell(H_ROW + 1 + r, NAME_COL).Value = Waves[r].Name;
            }
            mainWs.Columns(NAME_COL, NAME_COL).AdjustToContents();
            if (Hs.Count > 1) mainWs.Range(H_ROW - 1, NAME_COL + 1, H_ROW - 1, NAME_COL + Hs.Count).Merge();
            if (Waves.Count > 1) mainWs.Range(H_ROW + 1, NAME_COL - 1, H_ROW + Waves.Count, NAME_COL - 1).Merge();
            for (int c = 0; c < Hs.Count; c++)
                for (int r = 0; r < Waves.Count; r++)
                {
                    mainWs.Cell(H_ROW + 1 + r, NAME_COL + 1 + c).Value =
                        (Results.Single(a => a.Name == Waves[r].Name && a.H == Hs[c]).IsShow) ? "はい" : "いいえ";
                    mainWs.Cell(H_ROW + 1 + r, NAME_COL + 1 + c).DataValidation
                        .List($"\"はい,いいえ\"", true);
                    mainWs.Cell(H_ROW + 1 + r, NAME_COL + 1 + c).Style.Fill.BackgroundColor = XLColor.LightCyan;
                }
            mainWs.Range(H_ROW - 1, NAME_COL - 1, H_ROW + Waves.Count, NAME_COL + Hs.Count).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            mainWs.Range(H_ROW - 1, NAME_COL - 1, H_ROW + Waves.Count, NAME_COL + Hs.Count).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            wb.NamedRange("入力リスト").SetRefersTo(mainWs.Range(H_ROW - 1, NAME_COL - 1, H_ROW + Waves.Count, NAME_COL + Hs.Count));



            IXLWorksheet accWs = wb.Worksheet("加速度応答スペクトル");
            if (accWs == null) throw new ApplicationException("「加速度応答スペクトル」シートがありませんでした");
            IXLWorksheet velWs = wb.Worksheet("速度応答スペクトル");
            if (velWs == null) throw new ApplicationException("「速度応答スペクトル」シートがありませんでした");
            IXLWorksheet dispWs = wb.Worksheet("変位応答スペクトル");
            if (dispWs == null) throw new ApplicationException("「変位応答スペクトル」シートがありませんでした");
            for (int r = 0; r < Results[0].Data.Count; r++)
            {
                accWs.Cell(TITLE_ROW + 1 + r, FLEQ_COL).Value = Results[0].Data[r].Frequency;
                accWs.Cell(TITLE_ROW + 1 + r, SEQ_COL).Value = Results[0].Data[r].Second;
                velWs.Cell(TITLE_ROW + 1 + r, FLEQ_COL).Value = Results[0].Data[r].Frequency;
                velWs.Cell(TITLE_ROW + 1 + r, SEQ_COL).Value = Results[0].Data[r].Second;
                dispWs.Cell(TITLE_ROW + 1 + r, FLEQ_COL).Value = Results[0].Data[r].Frequency;
                dispWs.Cell(TITLE_ROW + 1 + r, SEQ_COL).Value = Results[0].Data[r].Second;
            }
            for (int c = 0; c < Results.Count; c++)
            {
                accWs.Cell(ID_ROW, DATA_COL + c).Value = $"{Results[c].Name}{Results[c].H}";
                velWs.Cell(ID_ROW, DATA_COL + c).Value = $"{Results[c].Name}{Results[c].H}";
                dispWs.Cell(ID_ROW, DATA_COL + c).Value = $"{Results[c].Name}{Results[c].H}";
                accWs.Cell(TITLE_ROW, DATA_COL + c).Value = Results[c].NameWithH;
                velWs.Cell(TITLE_ROW, DATA_COL + c).Value = Results[c].NameWithH;
                dispWs.Cell(TITLE_ROW, DATA_COL + c).Value = Results[c].NameWithH;
                for (int r = 0; r < Results[c].Data.Count; r++)
                {
                    accWs.Cell(TITLE_ROW + 1 + r, DATA_COL + c).Value = Results[c].Data[r].Acceleration;
                    velWs.Cell(TITLE_ROW + 1 + r, DATA_COL + c).Value = Results[c].Data[r].Velocity;
                    dispWs.Cell(TITLE_ROW + 1 + r, DATA_COL + c).Value = Results[c].Data[r].Displacement;
                }
            }
        }

        /// <summary>
        /// フォルダを選択
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void SelectOutputFolderExecute(Window param)
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog()
            {
                Title = "出力先を選択してください",
                InitialDirectory = OutputFolder,
                IsFolderPicker = true
            };
            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                OutputFolder = dlg.FileName;
                System.IO.Directory.SetCurrentDirectory(OutputFolder);
            }
        }
        private bool SelectOutputFolderCanExecute(Window param)
        {
            return true;
        }
        private RelayCommand<Window> _SelectOutputFolderCommand;
        public RelayCommand<Window> SelectOutputFolderCommand
        {
            get { return _SelectOutputFolderCommand = _SelectOutputFolderCommand ?? new RelayCommand<Window>(SelectOutputFolderExecute, SelectOutputFolderCanExecute); }
        }

        /// <summary>
        /// 波形を取り込む
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void ReadWavesExecute(Window param)
        {
            //MessageBox.Show("test");
            CommonOpenFileDialog dlg = new CommonOpenFileDialog()
            {
                Multiselect = true,
                Title = "データファイルを選択してください",
                InitialDirectory = OutputFolder,
                EnsureFileExists = true
            };
            dlg.Filters.Add(new CommonFileDialogFilter("データファイル", "dat"));
            dlg.Filters.Add(new CommonFileDialogFilter("All files", "*"));
            dlg.DefaultExtension = "dat";

            var accGroup = new CommonFileDialogGroupBox();
            var accLabel = new CommonFileDialogLabel() { Text = "加速度の単位" };
            accGroup.Items.Add(accLabel);
            CommonFileDialogRadioButtonList radio = new CommonFileDialogRadioButtonList();
            radio.Items.Add(new CommonFileDialogRadioButtonListItem("m/s2"));
            radio.Items.Add(new CommonFileDialogRadioButtonListItem("gal"));
            radio.Items.Add(new CommonFileDialogRadioButtonListItem("mm/s2"));
            radio.SelectedIndex = (int)Unit;
            accGroup.Items.Add(radio);
            dlg.Controls.Add(accGroup);

            var dtGroup = new CommonFileDialogGroupBox();
            var dtLabel = new CommonFileDialogLabel() { Text = "時間間隔[s]" };
            dtGroup.Items.Add(dtLabel);
            var dtText = new CommonFileDialogTextBox();
            dtText.Text = samplingPeriod.ToString();
            dtGroup.Items.Add(dtText);
            dlg.Controls.Add(dtGroup);

            dlg.FileOk += (object sender, CancelEventArgs e) =>
            {
                double d;
                if (double.TryParse(dtText.Text, out d))
                {
                    if (d <= 0)
                    {
                        MessageBox.Show("時間間隔は正の小数を入力してください。", "注意", MessageBoxButton.OK, MessageBoxImage.Warning);

                        e.Cancel = true;
                    }
                }
                else
                {
                    MessageBox.Show("時間間隔は正の小数を入力してください。", "注意", MessageBoxButton.OK, MessageBoxImage.Warning);
                    e.Cancel = true;
                }

            };

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                double dt;
                if (!double.TryParse(dtText.Text, out dt)) throw new Exception("時間間隔が小数として読めません");
                SamplingPeriod = dt;
                Unit = (UnitEnum)Enum.ToObject(typeof(UnitEnum), radio.SelectedIndex);
                double transAcc;
                switch (unit)
                {
                    case UnitEnum.m_s2:
                        transAcc = 1d;
                        break;
                    case UnitEnum.gal:
                        transAcc = 0.01;
                        break;
                    case UnitEnum.mm_s2:
                        transAcc = 0.001;
                        break;
                    default:
                        throw new Exception("加速度の単位が不明です");
                        //break;
                }

                foreach (string f in dlg.FileNames)
                {
                    Wave w = new Wave();
                    w.Name = System.IO.Path.GetFileNameWithoutExtension(f);
                    w.Dt = dt;
                    System.IO.StreamReader sr = System.IO.File.OpenText(f);
                    while (!sr.EndOfStream)
                    {
                        string s = sr.ReadLine();
                        double d;
                        if (double.TryParse(s, out d)) w.Data.Add(d * transAcc);
                    }
                    sr.Close();
                    w.CalculateMax();
                    Waves.Add(w);
                }
                CalculateCommand.RaiseCanExecuteChanged();
            }


        }
        private bool ReadWavesCanExecute(Window param)
        {
            return true;
        }
        private RelayCommand<Window> _ReadWavesCommand;
        public RelayCommand<Window> ReadWavesCommand
        {
            get { return _ReadWavesCommand = _ReadWavesCommand ?? new RelayCommand<Window>(ReadWavesExecute, ReadWavesCanExecute); }
        }


        /// <summary>
        /// 波形を表示
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void ShowWaveExecute(Wave param)
        {
            if (param == null) return;
            //if(WavePlotModel==null)
            //{
            WavePlotModel = new PlotModel() { Title = $"{param.Name}" };
            //軸設定
            WavePlotModel.Axes.Clear();
            OxyPlot.Axes.LinearAxis xLinearAxis = new OxyPlot.Axes.LinearAxis();
            xLinearAxis.Position = AxisPosition.Bottom;
            xLinearAxis.Title = "時間[s]";
            xLinearAxis.Key = "Time";
            xLinearAxis.MajorGridlineStyle = LineStyle.Automatic;
            xLinearAxis.MinorGridlineStyle = LineStyle.Automatic;
            WavePlotModel.Axes.Add(xLinearAxis);
            OxyPlot.Axes.LinearAxis yAxis = new OxyPlot.Axes.LinearAxis();
            yAxis.Position = AxisPosition.Left;
            yAxis.Title = "加速度[m/s2]";
            xLinearAxis.Key = "Acc";
            yAxis.MajorGridlineStyle = LineStyle.Automatic;
            yAxis.MinorGridlineStyle = LineStyle.Automatic;
            WavePlotModel.Axes.Add(yAxis);
            //}

            SelectedWave = param;

            WaveWindow dlg = new WaveWindow();
            dlg.DataContext = this;
            dlg.ShowDialog();
        }
        private bool ShowWaveCanExecute(Wave param)
        {
            return true;
        }
        private RelayCommand<Wave> _ShowWaveCommand;
        public RelayCommand<Wave> ShowWaveCommand
        {
            get { return _ShowWaveCommand = _ShowWaveCommand ?? new RelayCommand<Wave>(ShowWaveExecute, ShowWaveCanExecute); }
        }


        /// <summary>
        /// 波形をリストから削除
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void DeleteWaveExecute(object param)
        {
            //Debug.WriteLine($"{param}");
            if (param == null) return;
            Wave w = param as Wave;
            if (w == null) throw new ArgumentException("DeleteWaveExecute: invalid parameter.");
            Waves.Remove(w);
            CalculateCommand.RaiseCanExecuteChanged();
        }
        private bool DeleteWaveCanExecute(object param)
        {
            return true;
        }
        private RelayCommand<object> _DeleteWaveCommand;
        public RelayCommand<object> DeleteWaveCommand
        {
            get { return _DeleteWaveCommand = _DeleteWaveCommand ?? new RelayCommand<object>(DeleteWaveExecute, DeleteWaveCanExecute); }
        }

        private bool isCalculating = false;
        /// <summary>
        /// 応答スペクトルを計算
        /// </summary>
        /// <param name="param">The parameter.</param>
        private async void CalculateExecute(Object param)
        {
            isCalculating = true;
            CalculateCommand.RaiseCanExecuteChanged();

            AccPlotModel?.Series?.Clear();
            VelPlotModel?.Series?.Clear();
            DispPlotModel?.Series?.Clear();
            Results?.Clear();
            PlotColors.Clear();
            PlotLineStyles.Clear();

            double finishedProgress = 0d;

            var progress = new Progress<double>((d) =>
            {
                ProgressRate = d / Waves.Count + finishedProgress;
            });
            
            await Task.Run(() =>
            {
                for (int i = 0; i < Waves.Count; i++)
                {
                    //Waves[i].Dt = SamplingPeriod;
                    Debug.Assert(Waves[i].Dt > 0);
                    List<Spectra> anss = base.Calculation(Waves[i], progress).ToList();
                    foreach (var r in anss)
                    {
                        //results.Add(new SpectraViewModel(r));
                        Results.Add(new SpectraViewModel(r) { IsShow = true });
                    }
                    finishedProgress += 1d / (double)Waves.Count;
                }
            });

            for(int i=0; i<Waves.Count; i++)
            {
                int index = i % DEFAULT_COLORS.GetLength(0);
                PlotColors[Waves[i].Name] = OxyColor.FromRgb(DEFAULT_COLORS[index, 0], DEFAULT_COLORS[index, 1], DEFAULT_COLORS[index, 2]);
            }
            for(int i=0;i<Hs.Count;i++)
                PlotLineStyles[Hs[i]] = DEFAULT_LINESTYLES[i % DEFAULT_LINESTYLES.GetLength(0)];

            ShowResultCommand.RaiseCanExecuteChanged();
            isCalculating = false;
            CalculateCommand.RaiseCanExecuteChanged();
            StatusText = "完了しました";
            ProgressRate = 0d;

            ShowResultExecute(param);
        }
        private bool CalculateCanExecute(Object param)
        {
            return !isCalculating && Waves.Any();
        }
        private RelayCommand<Object> _CalculateCommand;
        public RelayCommand<Object> CalculateCommand
        {
            get { return _CalculateCommand = _CalculateCommand ?? new RelayCommand<Object>(CalculateExecute, CalculateCanExecute); }
        }

        /// <summary>
        /// 結果を表示
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void ShowResultExecute(Object param)
        {
            AccPlotModel = new PlotModel() { Title = "加速度応答スペクトル" };
            SetGraph(AccPlotModel,XAxisType, PlotType, YAxisType);
            AccPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "加速度[m/s2]";
            VelPlotModel = new PlotModel() { Title = "速度応答スペクトル" };
            SetGraph(VelPlotModel, XAxisType, PlotType, YAxisType);
            VelPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "速度[m/s]";
            DispPlotModel = new PlotModel() { Title = "変位応答スペクトル" };
            SetGraph(DispPlotModel, XAxisType, PlotType, YAxisType);
            DispPlotModel.Axes.FirstOrDefault(a => a.Position == AxisPosition.Left).Title = "変位[m]";

            DrawGraph();

            ResultWindow dlg = new ResultWindow();
            dlg.DataContext = this;

            Window w = param as Window;
            w?.Hide();
            dlg.ShowDialog();
            w?.Show();
            w?.Focus();
        }
        private bool ShowResultCanExecute(Object param)
        {
            return Results.Any();
        }
        private RelayCommand<Object> _ShowResultCommand;
        public RelayCommand<Object> ShowResultCommand
        {
            get { return _ShowResultCommand = _ShowResultCommand ?? new RelayCommand<Object>(ShowResultExecute, ShowResultCanExecute); }
        }


        /// <summary>
        /// 減衰をリストに追加
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void AddDampingExecute(string param)
        {
            double d;
            if (double.TryParse(param, out d))
            {
                if (d < 0 || d >= 1d)
                {
                    Debug.WriteLine($"{param}は0以上の値としてください。");
                    return;
                }
                if (base.Hs.Any(h => h == d))
                {
                    Debug.WriteLine($"{param}はすでにリストにあります。");
                    return;
                }
                base.Hs.Add(d);
                base.Hs = new ObservableCollection<double>(base.Hs.OrderBy(h => h));
                Debug.WriteLine($"Add: {param}");
                HInput = "";
            }
        }
        private bool AddDampingCanExecute(string param)
        {
            double d;
            if (!double.TryParse(hInput, out d)) return false;
            if (d < 0 || d >= 1d)return false;
            if (base.Hs.Any(h => h == d)) return false;
            return true;

                
        }
        private RelayCommand<string> _AddDampingCommand;
        public RelayCommand<string> AddDampingCommand
        {
            get { return _AddDampingCommand = _AddDampingCommand ?? new RelayCommand<string>(AddDampingExecute, AddDampingCanExecute); }
        }


        /// <summary>
        /// 減衰定数をリストから削除
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void DeleteDampingExecute(Object param)
        {
            if (param is double)
            {
                Debug.WriteLine($"Delete: {param}");
                base.Hs.Remove((double)param);
            }
        }
        private bool DeleteDampingCanExecute(Object param)
        {
            return true;
        }
        private RelayCommand<Object> _DeleteDampingCommand;
        public RelayCommand<Object> DeleteDampingCommand
        {
            get { return _DeleteDampingCommand = _DeleteDampingCommand ?? new RelayCommand<Object>(DeleteDampingExecute, DeleteDampingCanExecute); }
        }

        /// <summary>
        /// 応答スペクトルを再描画
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void RedrawGraphExecute(Object param)
        {
            DrawGraph();
        }
        private bool RedrawGraphCanExecute(Object param)
        {
            return isUpdateShownSpectrum;
        }
        private RelayCommand<Object> _RedrawGraphCommand;
        public RelayCommand<Object> RedrawGraphCommand
        {
            get { return _RedrawGraphCommand = _RedrawGraphCommand ?? new RelayCommand<Object>(RedrawGraphExecute, RedrawGraphCanExecute); }
        }

        /// <summary>
        /// 応答スペクトルの画像をクリップボードにコピー
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void CopyGraphExecute(OxyPlot.PlotModel param)
        {
            if (param == null) { Debug.Print("CopyGraphExecute: Parameter is null"); return; }
            var pngExporter = new PngExporter { Width = (int)param.Width, Height = (int)param.Height, Background = OxyColors.White };
            var bitmap = pngExporter.ExportToBitmap(param);
            Clipboard.SetImage(bitmap);

        }
        private bool CopyGraphCanExecute(OxyPlot.PlotModel param)
        {
            return true;
        }
        private RelayCommand<OxyPlot.PlotModel> _CopyGraphCommand;
        public RelayCommand<OxyPlot.PlotModel> CopyGraphCommand
        {
            get { return _CopyGraphCommand = _CopyGraphCommand ?? new RelayCommand<PlotModel>(CopyGraphExecute, CopyGraphCanExecute); }
        }


        /// <summary>
        /// 応答スペクトルの画像をファイルに保存
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void ExportGraphExecute(PlotModel param)
        {
            if (param == null)
            {
                Debug.Print("ExportGraphExecute: Parameter is null");
                return;
            }
            CommonSaveFileDialog dlg = new CommonSaveFileDialog()
            {
                Title = "保存するファイル名を指定してください",
                EnsurePathExists = true,
                DefaultExtension = "png",
                AlwaysAppendDefaultExtension = true,
                InitialDirectory = outputFolder,
                DefaultFileName = param.Title + ".png"
            };
            dlg.Filters.Add(new CommonFileDialogFilter("pngファイル", "png"));
            dlg.Filters.Add(new CommonFileDialogFilter("すべてのファイル", "*"));

            if (dlg.ShowDialog() == CommonFileDialogResult.Cancel) return;

            PngExporter pngExporter = new PngExporter { Width = (int)param.Width, Height = (int)param.Height, Background = OxyColors.White };

            pngExporter.ExportToFile(param, dlg.FileName);


        }
        private bool ExportGraphCanExecute(PlotModel param)
        {
            return true;
        }
        private RelayCommand<PlotModel> _ExportGraphCommand;
        public RelayCommand<PlotModel> ExportGraphCommand
        {
            get { return _ExportGraphCommand = _ExportGraphCommand ?? new RelayCommand<PlotModel>(ExportGraphExecute, ExportGraphCanExecute); }
        }


        /// <summary>
        /// 凡例の位置を変更
        /// </summary>
        /// <param name="param">The parameter.</param>
        private void SetLegendPositionExecute(Object param)
        {
            var t = param as Tuple<PlotModel, LegendPosition>;
            if (t == null)
            {
                Debug.Print("SetLegendPositionExecute: Parameter is invalied");
                return;
            }
            t.Item1.LegendPosition = t.Item2;
            t.Item1.InvalidatePlot(true);
        }
        private bool SetLegendPositionCanExecute(Object param)
        {
            return true;
        }
        private RelayCommand<Object> _SetLegendPositionCommand;
        public RelayCommand<Object> SetLegendPositionCommand
        {
            get { return _SetLegendPositionCommand = _SetLegendPositionCommand ?? new RelayCommand<Object>(SetLegendPositionExecute, SetLegendPositionCanExecute); }
        }


        public RelayCommand<Object> SetYAxisTypeCommand
        {
            get
            {
                return _SetYAxisTypeCommand = _SetYAxisTypeCommand ??
                  new RelayCommand<Object>((Object param) =>
                  {
                      var t = param as Tuple<PlotModel, AxisTypeEnum>;
                      if (t == null)
                      {
                          Debug.Print("SetYAxisTypeCommand: Parameter is invalied");
                          return;
                      }

                      OxyPlot.Axes.Axis yAxis =null;
                      switch (t.Item2)
                      {
                          case WaveProcessWPF.AxisTypeEnum.Liner:
                              yAxis = new OxyPlot.Axes.LinearAxis();
                              break;
                          case WaveProcessWPF.AxisTypeEnum.Logarithmic:
                              yAxis = new OxyPlot.Axes.LogarithmicAxis();
                              break;
                          default:
                              throw new ArgumentException($"SetYAxisTypeCommand: パラメータが違います: {t.Item2}");
                      }

                      yAxis.Title = t.Item1.Axes.SingleOrDefault(a => a.Position == AxisPosition.Left).Title;
                      t.Item1.Axes.Remove(t.Item1.Axes.SingleOrDefault(a => a.Position == AxisPosition.Left));

                      yAxis.Position = AxisPosition.Left;
                      yAxis.MajorGridlineStyle = LineStyle.Automatic;
                      yAxis.MinorGridlineStyle = LineStyle.Automatic;
                      t.Item1.Axes.Add(yAxis);
                      t.Item1.InvalidatePlot(true);

                  }, (Object param) =>
                  {
                      return true;
                  });
            }
        }
        private RelayCommand<Object> _SetYAxisTypeCommand;


        public RelayCommand<Object> SetXAxisTypeCommand
        {
            get
            {
                return _SetXAxisTypeCommand = _SetXAxisTypeCommand ??
                  new RelayCommand<Object>((Object param) =>
                  {
                      var t = param as Tuple<PlotModel, AxisTypeEnum>;
                      if (t == null)
                      {
                          Debug.Print("SetXAxisTypeCommand: Parameter is invalied");
                          return;
                      }

                      OxyPlot.Axes.Axis xAxis=null;
                      switch (t.Item2)
                      {
                          case AxisTypeEnum.Liner:
                              xAxis = new OxyPlot.Axes.LinearAxis();
                              break;
                          case AxisTypeEnum.Logarithmic:
                              xAxis = new OxyPlot.Axes.LogarithmicAxis();
                              break;
                          default:
                              throw new ArgumentException($"SetXAxisTypeCommand: パラメータが違います: {t.Item2}");
                      }
                      xAxis.Title = t.Item1.Axes.SingleOrDefault(a => a.Position == AxisPosition.Bottom).Title;
                      t.Item1.Axes.Remove(t.Item1.Axes.SingleOrDefault(a => a.Position == AxisPosition.Bottom));

                      xAxis.Position = AxisPosition.Bottom;
                      xAxis.MajorGridlineStyle = LineStyle.Automatic;
                      xAxis.MinorGridlineStyle = LineStyle.Automatic;
                      t.Item1.Axes.Add(xAxis);
                      t.Item1.InvalidatePlot(true);

                  }, (Object param) =>
                  {
                      return true;
                  });
            }
        }
        private RelayCommand<Object> _SetXAxisTypeCommand;


        public RelayCommand<Object> SetTimePeriodCommand
        {
            get
            {
                return _SetTimePeriodCommand = _SetTimePeriodCommand ??
                  new RelayCommand<Object>((Object param) =>
                  {
                      if (Enum.IsDefined(typeof(PlotTypeEnum), param) == false) return;
                      PlotTypeEnum t = (PlotTypeEnum)Enum.Parse(typeof(PlotTypeEnum), param.ToString());


                  }, (Object param) =>
                  {
                      return true;
                  });
            }
        }
        private RelayCommand<Object> _SetTimePeriodCommand;


        /// <summary>
        /// 結果をテキスト(スペース切り)で出力
        /// </summary>
        /// <value>
        /// The save result as text command.
        /// </value>
        public RelayCommand<Object> SaveResultAsTextCommand
        {
            get
            {
                return _SaveResultAsTextCommand = _SaveResultAsTextCommand ??
                  new RelayCommand<Object>((Object param) =>
                  {
                      OutputResultAsText();
                      MessageBox.Show($"{OutputFolder}に結果を保存しました。");
                      Process.Start(this.outputFolder); //フォルダを開く
                  }, 
                  (Object param) =>
                  {
                      return true;
                  });
            }
        }
        private RelayCommand<Object> _SaveResultAsTextCommand;


        /// <summary>
        /// 結果をCSVで出力
        /// </summary>
        /// <value>
        /// The save result as CSV command.
        /// </value>
        public RelayCommand<Window> SaveResultAsCsvCommand
        {
            get
            {
                return _SaveResultAsCsvCommand = _SaveResultAsCsvCommand ??
                  new RelayCommand<Window>((Window param) =>
                  {
                      OutputResultAsCsv();
                      MessageBox.Show($"{OutputFolder}に結果を保存しました。");
                      Process.Start(this.outputFolder); //フォルダを開く
                  }, (Window param) =>
                  {
                      return true;
                  });
            }
        }
        private RelayCommand<Window> _SaveResultAsCsvCommand;


        /// <summary>
        /// Excelで結果を保存する
        /// </summary>
        public RelayCommand<Object> SaveResultAsExcelCommand
        {
            get { return _SaveResultAsExcelCommand = _SaveResultAsExcelCommand ?? 
                    new RelayCommand<Object>((Object param)=> 
                    {
                        try
                        {
                            Uri uri = new Uri("/Results.xlsm", UriKind.Relative);
                            System.Windows.Resources.StreamResourceInfo info = Application.GetResourceStream(uri);
                            //StreamReader sr = new StreamReader(info.Stream);
                            XLWorkbook wb = new XLWorkbook(info.Stream);
                            OutputResultAsExcel(wb);
                            string xlsmPath = $"{OutputFolder}/RespSpecResults.xlsm";
                            wb.SaveAs(xlsmPath);
                            MessageBox.Show($"{xlsmPath}に結果を保存しました。");
                            Process.Start(xlsmPath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"アウトプットフォルダに保存できませんでした。\n {ex.Message}");
                        }

                    }, (Object param) => 
                    {
                        return true;
                    });
            }
        }
        private RelayCommand<Object> _SaveResultAsExcelCommand;



        /// <summary>
        /// htmlで結果を保存する
        /// </summary>
        /// <value>
        /// The save result as HTML command.
        /// </value>
        public RelayCommand<Window> SaveResultAsHtmlCommand
        {
            get
            {
                return _SaveResultAsHtmlCommand = _SaveResultAsHtmlCommand ??
                  new RelayCommand<Window>((Window param) =>
                  {
                      string folderName = "Report";
                      string filename = "RespSpec.html";

                      OutputResultAsHtml(this, filename, folderName);
                      MessageBox.Show($"{OutputFolder}に結果を保存しました。");
                      Process.Start($"{OutputFolder}\\{folderName}\\{filename}");
                  }, (Window param) =>
                  {
                      return true;
                  });
            }
        }
        private RelayCommand<Window> _SaveResultAsHtmlCommand;



    }
}
