using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using RealTimeMonitor.Model;
using Timer = System.Timers.Timer;

namespace RealTimeMonitor.ViewModel
{
    public class TrendViewModel : INotifyPropertyChanged
    {

        private readonly Timer _dataTimer;
        private readonly DataService _dataService = new DataService();
        private double _currentValue;
        private bool _isPaused;
        private double _xAxisMin;
        private double _xAxisMax;
        private DispatcherTimer _scrollTimer;

        // 添加滚动速度属性
        private double _scrollSpeed = 1.0;
        public double ScrollSpeed
        {
            get => _scrollSpeed;
            set
            {
                if (Math.Abs(_scrollSpeed - value) > 0.01)
                {
                    _scrollSpeed = Math.Max(0.1, Math.Min(5.0, value));
                    OnPropertyChanged(nameof(ScrollSpeed));
                }
            }
        }

        // 添加时间步长属性
        public double TimeStep => TimeSpan.FromSeconds(1).TotalMilliseconds; // 1秒间隔

        // 添加时间范围属性
        private double _timeRange = 20.0;
        public double TimeRange
        {
            get => _timeRange;
            set
            {
                if (Math.Abs(_timeRange - value) > 0.1)
                {
                    _timeRange = Math.Max(1.0, Math.Min(60.0, value));
                    OnPropertyChanged(nameof(TimeRange));
                }
            }
        }

        //监控变量名
        public string VariableName { get; }
        public SeriesCollection SeriesCollection { get; }
        public Func<double, string> YFormatter { get; } = value => value.ToString("F1");

        //横坐标左边起点
        public double XAxisMin
        {
            get => _xAxisMin;
            set
            {
                _xAxisMin = value;
                OnPropertyChanged(nameof(XAxisMin));
            }
        }

        //横坐标右边起点
        public double XAxisMax
        {
            get => _xAxisMax;
            set
            {
                _xAxisMax = value;
                OnPropertyChanged(nameof(XAxisMax));
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged(nameof(IsPaused));
                    if (_isPaused)
                    {
                        _dataTimer.Stop();
                        _scrollTimer.Stop();
                    }
                    else
                    {
                        _dataTimer.Start();
                        _scrollTimer.Start();
                    }
                }
            }
        }

        // 修改为可写属性
        private int _maxPoints = 200; // 默认200点
        public int MaxPoints
        {
            get => _maxPoints;
            set
            {
                if (_maxPoints != value)
                {
                    _maxPoints = value;
                    OnPropertyChanged(nameof(MaxPoints));
                    // 当最大点数改变时，可以添加额外逻辑
                    AdjustDataBuffer();
                }
            }
        }

        // 添加数据缓冲调整方法
        private void AdjustDataBuffer()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var values = SeriesCollection[0].Values as ChartValues<MeasureModel>;
                // 如果数据点超过新限制，移除多余的点
                while (values.Count > MaxPoints * 1.2)
                {
                    values.RemoveAt(0);
                }
            });
        }

       

       

        // 添加清空数据命令
        public ICommand ClearDataCommand { get; }
        public ICommand TogglePauseCommand { get; }

        private DateTime _startTime; // 添加起始时间基准
        private double _scrollOffset; // 滚动偏移量
       

        public TrendViewModel(string variableName)
        {
            VariableName = variableName;

            // 设置起始时间基准
            _startTime = DateTime.Now;

            // 配置图表映射器
            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.TimeOffset) // 使用相对时间偏移
                .Y(model => model.Value);

            Charting.For<MeasureModel>(mapper);

            // 初始化折线系列
            var lineSeries = new LineSeries
            {
                Title = variableName,
                Values = new ChartValues<MeasureModel>(),
                PointGeometry = null,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            SeriesCollection = new SeriesCollection { lineSeries };

            // 初始化数据更新定时器
            _dataTimer = new Timer(100) // 100ms更新间隔
            {
                AutoReset = true
            };
            _dataTimer.Elapsed += UpdateData;

            // 初始化滚动定时器
            _scrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20) // 50fps滚动
            };
            _scrollTimer.Tick += ScrollChart;

            // 设置初始X轴范围
            XAxisMin = 0;
            XAxisMax = TimeSpan.FromSeconds(20).TotalMilliseconds; // 初始显示20秒

            // 初始化命令
            ClearDataCommand = new RelayCommand(ClearData);
            TogglePauseCommand = new RelayCommand(TogglePause);

            // 启动定时器
            _dataTimer.Start();
            _scrollTimer.Start();
        }

        //暂停监控
        private void TogglePause(object parameter)
        {
            IsPaused = !IsPaused;
        }
        //清除数据
        private void ClearData(object parameter)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var values = SeriesCollection[0].Values as ChartValues<MeasureModel>;
                values.Clear();
            });
        }

        //定时刷新数据
        private void UpdateData(object sender, ElapsedEventArgs e)
        {
            if (IsPaused) return;

            _currentValue = _dataService.GetNextDataPoint();
            var now = DateTime.Now;
            var timeOffset = (now - _startTime).TotalMilliseconds; // 计算相对于起始时间的偏移

            Application.Current.Dispatcher.Invoke(() =>
            {
                var values = SeriesCollection[0].Values as ChartValues<MeasureModel>;
                values.Add(new MeasureModel
                {
                    TimeOffset = timeOffset,
                    Value = _currentValue
                });

                // 限制数据点数量（保留额外20%的点用于平滑过渡）
                while (values.Count > MaxPoints * 1.2)
                {
                    values.RemoveAt(0);
                }
            });
        }


        private void ScrollChart(object sender, EventArgs e)
        {
            if (IsPaused) return;

            // 更新滚动偏移
            _scrollOffset += _scrollSpeed * _scrollTimer.Interval.TotalMilliseconds;

            // 计算当前时间范围
            double currentMax = (DateTime.Now - _startTime).TotalMilliseconds;
            double currentMin = currentMax - TimeRange * 1000;

            // 应用平滑滚动
            XAxisMin = SmoothTransition(XAxisMin, currentMin);
            XAxisMax = SmoothTransition(XAxisMax, currentMax);
        }

        // 平滑过渡函数
        private double SmoothTransition(double current, double target)
        {
            // 使用缓动函数实现平滑过渡
            const double smoothingFactor = 0.2;
            return current + (target - current) * smoothingFactor;
        }

        // 时间格式化器 - 使用相对时间
        public Func<double, string> DateTimeFormatter => value =>
        {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(value);
            return $"{timeSpan.Seconds}.{timeSpan.Milliseconds:D3}s";
        };

        public void Cleanup()
        {
            _dataTimer.Stop();
            _dataTimer.Dispose();
            _scrollTimer.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 测量数据模型（带时间戳）
    /// </summary>
    public class MeasureModel
    {
        public double TimeOffset { get; set; } // 毫秒为单位的时间偏移
        public double Value { get; set; }
    }
}
