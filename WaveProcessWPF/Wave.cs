using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using System.Xml.Serialization.Configuration;

namespace WaveProcessWPF
{
    public class Wave :INotifyPropertyChanged
    {
        private List<double> data = new List<double>();
        private string name;
        private double dt;
        private double max;
        private double maxTime;

        public List<double> Data { get { return data; }
            set
            {
                data = value;
                RaisePropertyChanged();
                CalculateMax();
            }
        } 
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
        public double Dt
        {
            get { return dt;}
            set
            {
                if (value == dt) return;
                dt = value;
                CalculateMax();
                RaisePropertyChanged();
            }
        }

        public double Max { get { return max; } set { } }
        public double MaxTime { get { return maxTime; } set { } }

        public Wave()
        {

        }

        public void CalculateMax()
        {
            int maxIndex = !data.Any() ? -1 :
               data
               .Select((value, index) => new { Value = value, Index = index })
               .Aggregate((a, b) => (Math.Abs(a.Value) > Math.Abs(b.Value)) ? a : b)
               .Index;
            maxTime = maxIndex * dt;
            max = (maxIndex<0)?0: data[maxIndex];

            RaisePropertyChanged(nameof(Max));
            RaisePropertyChanged(nameof(MaxTime));
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
    }
}
