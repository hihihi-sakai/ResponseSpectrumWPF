using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;


namespace WaveProcessWPF
{
    public class Spectra: INotifyPropertyChanged
    {
        protected string name;
        protected double h;
        protected double dt;

        public string Name
        {
            get { return name; }
            set
            {
                if (value == name) return;
                name = value;
                RaisePropertyChanged();
            }
        }
        public double H
        {
            get { return h; }
            set
            {
                if (value == h) return;
                h = value;
                RaisePropertyChanged();
            }
        }
        public double Dt
        {
            get { return dt; }
            set
            {
                if (value == dt) return;
                dt = value;
                RaisePropertyChanged();
            }
        }
        public ObservableCollection<Spectrum> Data { get; set; } = new ObservableCollection<Spectrum>();

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
    }
}
