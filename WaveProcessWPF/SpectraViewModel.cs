namespace WaveProcessWPF
{
    public class SpectraViewModel : Spectra
    {
        private bool isShow = false;
        public bool IsShow
        {
            get { return isShow; }
            set
            {
                if (value == isShow) return;
                isShow = value;
                RaisePropertyChanged();
            }
        }

        public string NameWithH { get { return $"{Name} h={H.ToString()}";  } }

        public SpectraViewModel()
        {

        }

        public SpectraViewModel(Spectra spectra)
        {
            base.Data = spectra.Data;
            base.H = spectra.H;
            base.Name = spectra.Name;
            base.Dt = spectra.Dt;
        }


    }
}
