using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
using System.Windows.Shapes;
using LiveCharts.Wpf;

namespace RealTimeMonitor.View
{
    /// <summary>
    /// MultiTrendWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MultiTrendWindow : Window
    {
        public MultiTrendWindow()
        {
            InitializeComponent();

        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (DataContext is MultiTrendViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }
    public class SeriesToIdConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return null;

            if (values[0] is LineSeries series && values[1] is MultiTrendViewModel vm)
            {
                foreach (var kvp in vm._variableSeriesMap)
                {
                    if (kvp.Value == series)
                    {
                        return kvp.Key;
                    }
                }
            }

            return null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
