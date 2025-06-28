using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using LiveCharts;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Input;

namespace RealTimeMonitor.ViewModel
{
    public class NewTrendViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _dataTimer;
        private readonly DispatcherTimer _scrollTimer;
        private readonly ConcurrentQueue<DataPoint> _dataQueue = new ConcurrentQueue<DataPoint>();
        private DateTime _startTime;
        private double _xAxisMin;
        private double _xAxisMax;
        private double _targetXAxisMin;
        private double _targetXAxisMax;
        private bool _isPaused;
        private double _timeRange = 20.0; // 默认20秒
        private double _smoothFactor = 0.2;
        private double _scrollSpeed = 1.0;
        private int _frameCount;
        private DateTime _lastFrameTime = DateTime.Now;
        private int _pointCount;


        public string VariableName { get; set; }
        public SeriesCollection SeriesCollection { get; }
        public Func<double, string> YFormatter { get; } = value => value.ToString("F2");

        // 时间格式化器 - 显示秒和毫秒
        public Func<double, string> DateTimeFormatter => value =>
        {
            var time = TimeSpan.FromMilliseconds(value);
            return $"{time.Seconds}.{time.Milliseconds:D3}s";
        };

        public double XAxisMin
        {
            get => _xAxisMin;
            set
            {
                if (Math.Abs(_xAxisMin - value) > 0.1)
                {
                    _xAxisMin = value;
                    OnPropertyChanged(nameof(XAxisMin));
                }
            }
        }

        public double XAxisMax
        {
            get => _xAxisMax;
            set
            {
                if (Math.Abs(_xAxisMax - value) > 0.1)
                {
                    _xAxisMax = value;
                    OnPropertyChanged(nameof(XAxisMax));
                }
            }
        }

        public double TimeRange
        {
            get => _timeRange;
            set
            {
                // 限制在20-60秒范围内
                value = Math.Max(20, Math.Min(60, value));
                if (Math.Abs(_timeRange - value) > 0.1)
                {
                    _timeRange = value;
                    OnPropertyChanged(nameof(TimeRange));
                    UpdateTargetRange();
                }
            }
        }

        public double ScrollSpeed
        {
            get => _scrollSpeed;
            set
            {
                value = Math.Max(0.1, Math.Min(5.0, value));
                if (Math.Abs(_scrollSpeed - value) > 0.01)
                {
                    _scrollSpeed = value;
                    OnPropertyChanged(nameof(ScrollSpeed));
                    OnPropertyChanged(nameof(SpeedIndicator));
                }
            }
        }

        public string SpeedIndicator => $"滚动速度: {_scrollSpeed:F1}x";

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

        public string IsRunningIndicator => IsPaused ? "已暂停" : "运行中";
        public string PauseResumeText => IsPaused ? "继续监控" : "暂停监控";

        public int MaxPoints { get; set; } = 200;

        public int FrameRate { get; private set; }
        public int PointCount => _pointCount;

        public string DebugInfo => $"{DateTime.Now:HH:mm:ss.fff}\n" +
                                  $"帧率: {FrameRate} FPS | 点数: {PointCount}\n" +
                                  $"范围: {TimeRange:F0}s | 速度: {_scrollSpeed:F1}x";


        // 命令定义
        public ICommand TogglePauseCommand { get; }
        public ICommand ClearDataCommand { get; }
        public ICommand IncreaseSpeedCommand { get; }
        public ICommand DecreaseSpeedCommand { get; }
        public ICommand ResetSpeedCommand { get; }

        public NewTrendViewModel(string variableName)
        {
            VariableName = variableName;
            _startTime = DateTime.Now;

            // 配置图表映射器
            var mapper = Mappers.Xy<DataPoint>()
                .X(point => point.TimeOffset) // 使用时间偏移量
                .Y(point => point.Value);

            Charting.For<DataPoint>(mapper);

            // 初始化折线系列
            var lineSeries = new LineSeries
            {
                Title = variableName,
                Values = new ChartValues<DataPoint>(),
                PointGeometry = null,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            SeriesCollection = new SeriesCollection { lineSeries };

            // 初始化数据更新定时器
            _dataTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20fps
            };
            _dataTimer.Tick += ProcessDataQueue;

            // 初始化滚动定时器
            _scrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20) // 50fps
            };
            _scrollTimer.Tick += SmoothScroll;

            // 设置初始X轴范围
            ResetTimeRange();

            // 初始化命令
            TogglePauseCommand = new RelayCommand(TogglePause);
            ClearDataCommand = new RelayCommand(ClearData);
            IncreaseSpeedCommand = new RelayCommand(IncreaseSpeed);
            DecreaseSpeedCommand = new RelayCommand(DecreaseSpeed);
            ResetSpeedCommand = new RelayCommand(ResetSpeed);


            // 启动定时器
            _dataTimer.Start();
            _scrollTimer.Start();
        }

        // 添加数据点（线程安全）
        public void AddDataPoint(double value)
        {
            var now = DateTime.Now;
            var timeOffset = (now - _startTime).TotalMilliseconds;

            _dataQueue.Enqueue(new DataPoint
            {
                TimeOffset = timeOffset,
                Value = value
            });
        }

        // 处理数据队列
        private void ProcessDataQueue(object sender, EventArgs e)
        {
            if (IsPaused) return;

            // 处理队列中的所有数据点
            while (_dataQueue.TryDequeue(out var dataPoint))
            {
                SeriesCollection[0].Values.Add(dataPoint);

                // 限制数据点数量
                if (SeriesCollection[0].Values.Count > MaxPoints * 1.2)
                {
                    SeriesCollection[0].Values.RemoveAt(0);
                }
            }

            // 更新点数统计
            _pointCount = SeriesCollection[0].Values.Count;
            OnPropertyChanged(nameof(PointCount));
        }

        // 平滑滚动时间轴
        private void SmoothScroll(object sender, EventArgs e)
        {
            if (IsPaused) return;

            // 更新帧率统计
            _frameCount++;
            var now = DateTime.Now;
            if ((now - _lastFrameTime).TotalSeconds >= 1)
            {
                FrameRate = _frameCount;
                _frameCount = 0;
                _lastFrameTime = now;
                OnPropertyChanged(nameof(DebugInfo));
            }

            // 更新目标范围
            UpdateTargetRange();

            // 应用平滑过渡
            XAxisMin = XAxisMin + (_targetXAxisMin - XAxisMin) * _smoothFactor;
            XAxisMax = XAxisMax + (_targetXAxisMax - XAxisMax) * _smoothFactor;

            OnPropertyChanged(nameof(FrameRate));
        }

        // 更新目标时间范围
        private void UpdateTargetRange()
        {
            var now = DateTime.Now;
            var currentTime = (now - _startTime).TotalMilliseconds;

            // 计算目标范围（考虑滚动速度）
            _targetXAxisMin = currentTime - TimeRange * 1000 * _scrollSpeed;
            _targetXAxisMax = currentTime;
        }

        // 重置时间范围
        private void ResetTimeRange()
        {
            var now = DateTime.Now;
            var currentTime = (now - _startTime).TotalMilliseconds;

            XAxisMin = currentTime - TimeRange * 1000;
            XAxisMax = currentTime;

            _targetXAxisMin = XAxisMin;
            _targetXAxisMax = XAxisMax;
        }

        // 暂停
        private void TogglePause(object parameter)
        {
            IsPaused = !IsPaused;
        }

        // 清空数据
        private void ClearData(object parameter)
        {
            SeriesCollection[0].Values.Clear();
            while (_dataQueue.TryDequeue(out _)) { }
            _startTime = DateTime.Now;
            ResetTimeRange();
            _pointCount = 0;
            OnPropertyChanged(nameof(PointCount));
            OnPropertyChanged(nameof(DebugInfo));
        }

        private void IncreaseSpeed(object parameter)
        {
            ScrollSpeed = Math.Min(5.0, ScrollSpeed + 0.5);
        }

        private void DecreaseSpeed(object parameter)
        {
            ScrollSpeed = Math.Max(0.1, ScrollSpeed - 0.5);
        }

        private void ResetSpeed(object parameter)
        {
            ScrollSpeed = 1.0;
        }
        public void Cleanup()
        {
            _dataTimer.Stop();
            _scrollTimer.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    /// <summary>
    /// 数据点模型
    /// </summary>
    public class DataPoint
    {
        public double TimeOffset { get; set; } // 毫秒为单位
        public double Value { get; set; }
    }
}
