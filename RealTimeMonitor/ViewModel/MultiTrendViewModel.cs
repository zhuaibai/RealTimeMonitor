using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveCharts.Wpf;
using LiveCharts;
using RealTimeMonitor.ViewModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows;

namespace RealTimeMonitor.View
{
    public class MultiTrendViewModel : INotifyPropertyChanged
    {
        private readonly DispatcherTimer _scrollTimer;
        private DateTime _startTime;
        private double _xAxisMin;
        private double _xAxisMax;
        private double _targetXAxisMin;
        private double _targetXAxisMax;
        private double _smoothFactor = 0.2;
        private double _scrollSpeed = 1.0;
        private int _frameCount;
        private DateTime _lastFrameTime = DateTime.Now;
        private bool _isPaused;
        private double _timeRange = 20.0;
        public Guid Id;

        // 存储变量与系列的映射关系
        public Dictionary<Guid, LineSeries> _variableSeriesMap = new Dictionary<Guid, LineSeries>();

        public SeriesCollection SeriesCollection { get; } = new SeriesCollection();
        

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
                    OnPropertyChanged(nameof(IsRunningIndicator));
                    OnPropertyChanged(nameof(PauseResumeText));

                    if (_isPaused)
                    {
                        _scrollTimer.Stop();
                    }
                    else
                    {
                        _scrollTimer.Start();
                    }
                }
            }
        }

        public string IsRunningIndicator => IsPaused ? "已暂停" : "运行中";
        public string PauseResumeText => IsPaused ? "继续监控" : "暂停监控";

        public int FrameRate { get; private set; }

        public string DebugInfo => $"{DateTime.Now:HH:mm:ss.fff}\n" +
                                  $"帧率: {FrameRate} FPS\n" +
                                  $"范围: {TimeRange:F0}s | 速度: {_scrollSpeed:F1}x";

        // 命令定义
        public ICommand TogglePauseCommand { get; }
        public ICommand ClearDataCommand { get; }
        public ICommand IncreaseSpeedCommand { get; }
        public ICommand DecreaseSpeedCommand { get; }
        public ICommand ResetSpeedCommand { get; }
        public ICommand ToggleVisibilityCommand { get; }

        public MultiTrendViewModel(List<VariableItem> variables)
        {
            _startTime = DateTime.Now;
            _lastFrameTime = DateTime.Now;

            // 为每个变量创建系列
            int colorIndex = 0;
            var colors = new Color[]
            {
                Colors.DodgerBlue,
                Colors.Orange,
                Colors.Green,
                Colors.Red,
                Colors.Purple,
                Colors.Brown,
                Colors.Teal,
                Colors.Magenta,
                Colors.Gold,
                Colors.DeepSkyBlue
            };

            foreach (var variable in variables)
            {
                var lineSeries = new LineSeries
                {
                    Title = variable.Name,
                    Values = new ChartValues<DataPoint>(),
                    PointGeometry = null,
                    Stroke = new SolidColorBrush(colors[colorIndex % colors.Length]),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                };

                SeriesCollection.Add(lineSeries);
                _variableSeriesMap[variable.Id] = lineSeries;

                // 订阅变量更新事件
                variable.PropertyChanged += Variable_PropertyChanged;

                colorIndex++;
            }

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
            ToggleVisibilityCommand = new RelayCommand(ToggleVisibility);

            // 启动定时器
            _scrollTimer.Start();
        }

        public Func<double, string> YFormatter { get; } = value => value.ToString("F2");

        // 时间格式化器
        public Func<double, string> DateTimeFormatter => value =>
        {
            var time = TimeSpan.FromMilliseconds(value);
            return $"{time.Seconds}.{time.Milliseconds:D3}s";
        };

        // 处理变量值更新
        private void Variable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(VariableItem.CurrentValue)) return;
            if (sender is not VariableItem variable) return;

            var now = DateTime.Now;
            var timeOffset = (now - _startTime).TotalMilliseconds;

            if (_variableSeriesMap.TryGetValue(variable.Id, out var series))
            {
                var dataPoint = new DataPoint
                {
                    TimeOffset = timeOffset,
                    Value = variable.CurrentValue
                };

                // 在UI线程添加数据点
                Application.Current.Dispatcher.Invoke(() =>
                {
                    series.Values.Add(dataPoint);

                    // 限制数据点数量
                    if (series.Values.Count > 200 * 1.2)
                    {
                        series.Values.RemoveAt(0);
                    }
                });
            }
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

        // 命令执行方法
        private void TogglePause(object parameter)
        {
            IsPaused = !IsPaused;
        }

        private void ClearData(object parameter)
        {
            foreach (var series in SeriesCollection)
            {
                series.Values.Clear();
            }
            _startTime = DateTime.Now;
            ResetTimeRange();
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

        private void ToggleVisibility(object parameter)
        {
            if (parameter is Guid variableId && _variableSeriesMap.TryGetValue(variableId, out var series))
            {
                series.Visibility = series.Visibility == Visibility.Visible
                    ? Visibility.Hidden
                    : Visibility.Visible;
            }
        }

        public void Cleanup()
        {
            _scrollTimer.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
