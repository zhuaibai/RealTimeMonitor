using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Serialization;
using RealTimeMonitor.SerialPortTools;

namespace RealTimeMonitor.ViewModel
{
    public class SerialPortSettingViewModel:INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private SerialPortSettings _settings;

        public ObservableCollection<string> AvailablePorts { get; }
        public ObservableCollection<int> BaudRates { get; }
        public ObservableCollection<int> DataBits { get; }
        public ObservableCollection<StopBits> StopBitsOptions { get; }
        public ObservableCollection<Parity> ParityOptions { get; }

        public string SelectedPort
        {
            get { return _settings.PortName; }
            set
            {
                if (_settings.PortName != value)
                {
                    _settings.PortName = value;
                    OnPropertyChanged(nameof(SelectedPort));
                }
            }
        }

        public int SelectedBaudRate
        {
            get { return _settings.BaudRate; }
            set
            {
                if (_settings.BaudRate != value)
                {
                    _settings.BaudRate = value;
                    OnPropertyChanged(nameof(SelectedBaudRate));
                }
            }
        }

        public int SelectedDataBits
        {
            get { return _settings.DataBits; }
            set
            {
                if (_settings.DataBits != value)
                {
                    _settings.DataBits = value;
                    OnPropertyChanged(nameof(SelectedDataBits));
                }
            }
        }

        public StopBits SelectedStopBits
        {
            get { return _settings.StopBits; }
            set
            {
                if (_settings.StopBits != value)
                {
                    _settings.StopBits = value;
                    OnPropertyChanged(nameof(SelectedStopBits));
                }
            }
        }

        public Parity SelectedParity
        {
            get { return _settings.Parity; }
            set
            {
                if (_settings.Parity != value)
                {
                    _settings.Parity = value;
                    OnPropertyChanged(nameof(SelectedParity));
                }
            }
        }

        public ICommand SaveCommand { get; }

        public SerialPortSettingViewModel()
        {
            _settings = LoadSettings();

            AvailablePorts = new ObservableCollection<string>(SerialPort.GetPortNames());
            BaudRates = new ObservableCollection<int> { 2400, 9600, 115200 };
            DataBits = new ObservableCollection<int> { 7, 8 };
            StopBitsOptions = new ObservableCollection<StopBits> { StopBits.One, StopBits.Two };
            ParityOptions = new ObservableCollection<Parity> { Parity.None, Parity.Even, Parity.Odd };


            SaveCommand = new RelayCommand(SaveSettings);
        }

        private SerialPortSettings LoadSettings()
        {
            try
            {
                if (File.Exists("serialSettings.xml"))
                {
                    var serializer = new XmlSerializer(typeof(SerialPortSettings));
                    using (var reader = new StreamReader("serialSettings.xml"))
                    {
                        return (SerialPortSettings)serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载设置时出错: {ex.Message}");
                return new SerialPortSettings();
            }
            return new SerialPortSettings();


        }

        private void SaveSettings(object parameter)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(SerialPortSettings));
                using (var writer = new StreamWriter("serialSettings.xml"))
                {
                    serializer.Serialize(writer, _settings);
                }
                Console.WriteLine("设置已保存");
                MessageBox.Show("设置已保存,请重新打开串口生效!\r\nThe settings have been saved, please re-open the serial port to take effect!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存设置时出错: {ex.Message}");
                MessageBox.Show($"保存设置时出错:{ex.Message}");
            }
        }
    }
}
