namespace WaveProcessWPF
{
    public class Spectrum
    {
        public double Frequency { get; set; }
        public double Second {
            get { return 1d / Frequency; }
            set { Frequency = 1 / value; }
        }
        public double Acceleration { get; set; }
        public double Velocity { get; set; }
        public double Displacement { get; set; }
    }
}
