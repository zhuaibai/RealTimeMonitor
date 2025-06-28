using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealTimeMonitor.ConvertTools
{
    internal class ConvertToolscs
    {
    }

    /// <summary>
    /// 布尔值到暂停/继续文本转换器
    /// </summary>
    public class BoolToPauseResumeConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "继续监控" : "暂停监控";
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
