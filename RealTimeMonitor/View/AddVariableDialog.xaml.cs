﻿using System;
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
using System.Windows.Shapes;
using RealTimeMonitor.ViewModel;

namespace RealTimeMonitor.View
{
    /// <summary>
    /// AddVariableDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddVariableDialog : Window
    {
        public AddVariableDialog()
        {
            InitializeComponent();
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddVariableViewModel vm)
            {
                vm.CloseAction = () => DialogResult = true;
            }
        }
    }
}
