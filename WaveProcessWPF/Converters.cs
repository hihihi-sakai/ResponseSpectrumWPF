using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace WaveProcessWPF
{
    public class EnumBooleanConverter : IValueConverter
    {
        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string parameterString = parameter as string;
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            if (Enum.IsDefined(value.GetType(), value) == false)
                return DependencyProperty.UnsetValue;

            object parameterValue = Enum.Parse(value.GetType(), parameterString);

            return parameterValue.Equals(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string parameterString = parameter as string;
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            return Enum.Parse(targetType, parameterString);
        }
        #endregion
    }

    public class LegendPositionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            Tuple<PlotModel, LegendPosition> tuple = new Tuple<PlotModel, LegendPosition>(
                (PlotModel)values[0], (LegendPosition)values[1]);
            return (object)tuple;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            var param = (Tuple<PlotModel, LegendPosition>)value;
            return new object[] { param.Item1, param.Item2 };
        }
    }

    //public class AxisTypeConverter : IMultiValueConverter
    //{
    //    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        Tuple<PlotModel, AxisTypeEnum> tuple = new Tuple<PlotModel, AxisTypeEnum>(
    //            (PlotModel)values[0], (AxisTypeEnum)values[1]);
    //        return (object)tuple;
    //    }

    //    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        var param = (Tuple<PlotModel, AxisTypeEnum>)value;
    //        return new object[] { param.Item1, param.Item2 };
    //    }
    //}


    //public class PlotTypeConverter : IMultiValueConverter
    //{
    //    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        Tuple<PlotModel, PlotTypeEnum> tuple = new Tuple<PlotModel, PlotTypeEnum>(
    //            (PlotModel)values[0], (PlotTypeEnum)values[1]);
    //        return (object)tuple;
    //    }

    //    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        var param = (Tuple<PlotModel, PlotTypeEnum>)value;
    //        return new object[] { param.Item1, param.Item2 };
    //    }
    //}
}
