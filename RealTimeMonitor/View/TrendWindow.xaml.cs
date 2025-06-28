using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using RealTimeMonitor.ViewModel;

namespace RealTimeMonitor.View
{
    /// <summary>
    /// TrendWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TrendWindow : Window
    {
        public TrendWindow()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (DataContext is TrendViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }

    
}
