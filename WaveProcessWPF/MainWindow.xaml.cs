using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WaveProcessWPF
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public RespSpectrumViewModel Model { get; set; }

        public MainWindow()
        {
            Model = new RespSpectrumViewModel();
            this.DataContext = Model;

            InitializeComponent();

            WaveExpander.IsExpanded = true;
            DampingExpender.IsExpanded = true;
            FrequencyExpander.IsExpanded = true;


        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ((RespSpectrumViewModel)this.DataContext).Closing();
        }

        private void WaveListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((RespSpectrumViewModel)this.DataContext).DeleteWaveCommand.RaiseCanExecuteChanged();
        }
    }
}
