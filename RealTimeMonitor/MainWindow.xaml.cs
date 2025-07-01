using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using RealTimeMonitor.ViewModel;
using WpfApp1.Views;

namespace RealTimeMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainViewModel();
            Loaded += OnLoaded;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 将DataGrid引用传递给ViewModel
            if (DataContext is MainViewModel vm)
            {
                vm.VariablesGrid = VariablesGrid;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Cleanup();
            }
        }

        /// <summary>
        /// 串口弹出界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SerialSettingButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SerialPortSettingWindow() { Owner = this };

            settingsWindow.ShowDialog();
        }

        
    }
    /// <summary>
    /// 布尔值反转转换器
    /// </summary>
    public class BoolInverterConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }
    }

    public class IntegerValidationRule : ValidationRule
    {
        public int Min { get; set; } = int.MinValue;
        public int Max { get; set; } = int.MaxValue;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value == null)
                return new ValidationResult(false, "值不能为空");

            if (!int.TryParse(value.ToString(), out int intValue))
                return new ValidationResult(false, "请输入有效的整数");

            if (intValue < Min)
                return new ValidationResult(false, $"值不能小于 {Min}");

            if (intValue > Max)
                return new ValidationResult(false, $"值不能大于 {Max}");

            return ValidationResult.ValidResult;
        }
    }
}