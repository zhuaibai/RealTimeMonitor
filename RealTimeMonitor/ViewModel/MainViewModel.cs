using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using Microsoft.Win32;
using RealTimeMonitor.Model;
using RealTimeMonitor.SerialPortTools;
using RealTimeMonitor.View;
using Timer = System.Timers.Timer;

namespace RealTimeMonitor.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        #region 常规属性

        // 使用线程安全的集合
        private readonly ConcurrentDictionary<Guid, VariableItem> _variablesDict = new();
        private readonly DataService _dataService = new DataService();
        private Timer _updateTimer;
        private VariableItem _selectedVariable;
        private int _variableCount;
        // 存储已打开的趋势窗口
        private readonly Dictionary<Guid, NewTrendWindow> _openTrendWindows = new();

        // 添加/删除变量时使用同步
        private readonly object _syncLock = new();

        //监控变量
        public ObservableCollection<VariableItem> Variables { get; } = new ObservableCollection<VariableItem>();

        //监控变量名称
        public ObservableCollection<string> VariableNames { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> Types { get; set; } = new ObservableCollection<string>() { "int8U", "int16U", "int32U", "int8S", "int16S", "int32S" };

        // 创建字典存储名称-地址映射
        Dictionary<string, string> addressMap = new Dictionary<string, string>();

        // 创建字典存储名称-偏移地址映射(大小)
        Dictionary<string, string> addressMoveMap = new Dictionary<string, string>();

        // 添加多选支持
        private ObservableCollection<VariableItem> _selectedVariables = new ObservableCollection<VariableItem>();
        public ObservableCollection<VariableItem> SelectedVariables
        {
            get => _selectedVariables;
            set
            {
                _selectedVariables = value;
                OnPropertyChanged(nameof(SelectedVariables));
                OnPropertyChanged(nameof(IsAnyVariableSelected));
            }
        }

        public bool IsAnyVariableSelected => SelectedVariables.Count() > 0;

        public ICommand ShowMultiTrendCommand { get; }

        /// <summary>
        /// 数据类型
        /// </summary>
        private string _type = "int8U";

        public string SelectedType
        {
            get { return _type; }
            set
            {
                _type = value;
                this.RaiseProperChanged(nameof(SelectedType));
            }
        }


        //map文件名字
        private string _FileName;

        public string FileName
        {
            get { return _FileName; }
            set
            {
                _FileName = value;
                this.OnPropertyChanged(nameof(FileName));
            }
        }

        //需要查找的监控变量
        private string _findName;

        public string FindName
        {
            get { return _findName; }
            set
            {
                _findName = value;
                this.OnPropertyChanged(nameof(FindName));
            }
        }

        //对应监控变量的地址
        private string paraPath;

        public string ParaPath
        {
            get { return paraPath; }
            set
            {
                paraPath = value;
                this.OnPropertyChanged(nameof(ParaPath));
            }
        }

        public int VariableCount
        {
            get => _variableCount;
            set
            {
                if (_variableCount != value)
                {
                    _variableCount = value;
                    OnPropertyChanged(nameof(VariableCount));
                }
            }
        }

        public VariableItem SelectedVariable
        {
            get => _selectedVariable;
            set
            {
                if (_selectedVariable != value)
                {
                    _selectedVariable = value;
                    OnPropertyChanged(nameof(SelectedVariable));
                    OnPropertyChanged(nameof(IsVariableSelected));
                }
            }
        }

        public bool IsVariableSelected => SelectedVariable != null;


        public ICommand CommitEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand AddVariableCommand { get; }
        public ICommand AddNewVariableCommand { get; }
        public ICommand DeleteVariableCommand { get; }
        public ICommand ShowTrendCommand { get; }
        public ICommand ToggleMonitoringCommand { get; }

        public ICommand GetVariableItems { get; }
        public ICommand GetVariablePath { get; }
        public ICommand OpenOrCloseCom { get; }

        #endregion

        #region 对Map文件处理方法
        // <summary>
        // 提取监控变量名和地址
        // </summary>
        public void GetVariableName(object obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Map Files (*.map)|*.map|All Files (*.*)|*.*",
                Title = "Select a Map File"
            };
            if (openFileDialog.ShowDialog() == true)
            {


                string filePath = openFileDialog.FileName;
                FileName = Path.GetFileName(filePath);
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(filePath);
                    List<string> dataLines = ExtractDataLines(lines);
                    List<string> dataLines2 = ExtractDataLines2(lines);
                    // 清空原有项
                    VariableNames.Clear();
                    // 添加新项（保留相同的集合引用）
                    foreach (var item in dataLines)
                    {
                        VariableNames.Add(item);
                    }


                    //处理集合到Map
                    GetDictionary();
                    GetDictionary(dataLines2);
                    //重新设置监控变量名
                    VariableNames.Clear();

                    foreach (var item in addressMap.Keys.ToList())
                    {
                        VariableNames.Add(item);
                    }

                    //获取偏移地址

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

        }


        /// <summary>
        /// 提取Map文件中的Data数据方法(变量名 地址)
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        private List<string> ExtractDataLines(string[] lines)
        {
            List<string> result = new List<string>();
            string previous = ""; // 上一行的开头

            foreach (string line in lines)
            {
                // 跳过空行
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // 确保parts数组有足够的元素
                if (parts.Length == 0)
                    continue;

                if (line.Contains("Data"))
                {
                    string entry = parts[0].TrimStart(); // 第一个元素应该总是存在（前面检查过）

                    if (entry.StartsWith("0x"))
                    {
                        // Ox开头说明前面没有变量名，需要加上上一行开头
                        result.Add(previous + " " + entry);
                    }
                    else if (parts.Length >= 2 && parts[1].TrimStart().StartsWith("0x"))
                    {
                        // 确保有第二个元素再访问
                        if (string.IsNullOrEmpty(parts[0].Trim()))
                        {
                            result.Add(previous + " " + parts[1].Trim());
                        }
                        else
                        {
                            result.Add(parts[0] + " " + parts[1]);
                        }
                    }
                }
                else
                {
                    // 保存这一行的开头（已经确保parts.Length >= 1）
                    previous = parts[0];
                }
            }

            return result;
        }

        /// <summary>
        /// 提取Map文件中的Data数据方法(变量名 偏移地址)
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        private List<string> ExtractDataLines2(string[] lines)
        {
            List<string> result = new List<string>();
            string previous = ""; // 上一行的开头

            foreach (string line in lines)
            {
                // 跳过空行
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // 确保parts数组有足够的元素
                if (parts.Length == 0)
                    continue;

                if (line.Contains("Data"))
                {
                    string entry = parts[0].TrimStart(); // 第一个元素应该总是存在（前面检查过）

                    if (entry.StartsWith("0x"))
                    {
                        // Ox开头说明前面没有变量名，需要加上上一行开头
                        result.Add(previous + " " + parts[1].TrimStart());
                    }
                    else if (parts.Length >= 2 && parts[1].TrimStart().StartsWith("0x"))
                    {
                        // 确保有第二个元素再访问
                        if (string.IsNullOrEmpty(parts[0].Trim()))
                        {
                            result.Add(previous + " " + parts[2].Trim());
                        }
                        else
                        {
                            result.Add(parts[0] + " " + parts[2]);
                        }
                    }
                }
                else
                {
                    // 保存这一行的开头（已经确保parts.Length >= 1）
                    previous = parts[0];
                }
            }

            return result;
        }

        /// <summary>
        /// 把集合转成Map形式（变量名 + 地址）
        /// </summary>
        private void GetDictionary()
        {
            addressMap.Clear();
            // 处理每个元素
            foreach (var item in VariableNames)
            {
                // 查找第一个空格位置
                int firstSpaceIndex = item.IndexOf(' ');

                if (firstSpaceIndex > 0) // 确保找到有效空格
                {
                    // 分割名称和地址
                    string name = item.Substring(0, firstSpaceIndex);
                    string address = item.Substring(firstSpaceIndex + 1);

                    // 添加到字典（重复名称会覆盖旧值）
                    addressMap[name] = address;
                }
            }
        }

        /// <summary>
        /// 把集合转成Map形式（变量名 + 偏移量）
        /// </summary>
        private void GetDictionary(List<string> items)
        {
            // 处理每个元素
            foreach (var item in items)
            {
                // 查找第一个空格位置
                int firstSpaceIndex = item.IndexOf(' ');

                if (firstSpaceIndex > 0) // 确保找到有效空格
                {
                    // 分割名称和地址
                    string name = item.Substring(0, firstSpaceIndex);
                    string address = item.Substring(firstSpaceIndex + 1);

                    // 添加到字典（重复名称会覆盖旧值）
                    addressMoveMap[name] = address;
                }
            }
        }

        /// <summary>
        /// 根据监控变量名获取地址
        /// </summary>
        private void GetPathByName(object obj)
        {
            if (addressMap.TryGetValue(FindName, out string result))
            {
                ParaPath = result;
            }
            else
            {
                MessageBox.Show($"找不到该变量{FindName}");
            }
        }

        /// <summary>
        /// 判断变量存不存在
        /// </summary>
        /// <param name="fin"></param>
        /// <returns></returns>
        private bool HaveFindName(string fin)
        {
            if (string.IsNullOrEmpty(fin))
            {
                MessageBox.Show("请选择需要监控的变量");
                return false;
            }
            if (addressMap.TryGetValue(fin, out string result))
            {
                return true;
            }
            else
            {
                MessageBox.Show("未找到需要监控的变量");
                return false;
            }
        }

        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数
        /// </summary>
        public MainViewModel()
        {
            //初始化串口信息
            IniCom();
            // 初始化示例变量

            VariableCount = Variables.Count;

            // 监听集合变化
            Variables.CollectionChanged += (s, e) =>
            {
                VariableCount = Variables.Count;
                OnPropertyChanged(nameof(IsVariableSelected));

                // 当删除选中项时，清除选中状态
                if (e.Action == NotifyCollectionChangedAction.Remove &&
                    e.OldItems.Contains(SelectedVariable))
                {
                    SelectedVariable = null;
                }
            };

            // 初始化命令
            AddVariableCommand = new RelayCommand(AddVariable);
            AddNewVariableCommand = new RelayCommand(AddNewVariable);
            DeleteVariableCommand = new RelayCommand(DeleteVariable, CanDeleteVariable);
            ShowTrendCommand = new RelayCommand(ShowTrend, CanShowTrend);
            ToggleMonitoringCommand = new RelayCommand(ToggleMonitoring);
            GetVariableItems = new RelayCommand(GetVariableName);
            GetVariablePath = new RelayCommand(GetPathByName);
            OpenOrCloseCom = new RelayCommand(openCom);
            // 初始化多趋势命令
            ShowMultiTrendCommand = new RelayCommand(ShowMultiTrend,CanShowMultiTrend);
            ShowDemoCoammand = new RelayCommand(ShowDemo);


            // 启动定时更新数据(随机生成数据)
            //_updateTimer = new Timer(1000);
            //_updateTimer.Elapsed += UpdateVariables;
            //_updateTimer.Start();


        }

        #endregion

        #region 监控变量处理方法

        public ICommand ShowDemoCoammand { get; }

        private void ShowDemo(object sender)
        {
            new Window1().Show();
        }

        private bool CanShowMultiTrend(object parameter) => IsVariableSelected;

        /// <summary>
        /// 显示多变量实时曲线
        /// </summary>
        /// <param name="parameter"></param>
        private void ShowMultiTrend(object parameter)
        {
            if (SelectedVariables.Count == 0) return;
            if (SelectedVariables.Count == 1)
            {
                ShowTrend(SelectedVariable);
                return;
            }
            var MultiTrendWindow = new MultiTrendViewModel(SelectedVariables.ToList());
            // 创建多变量趋势窗口
            var trendWindow = new MultiTrendWindow
            {
                DataContext = MultiTrendWindow
            };
            trendWindow.Show();
        }

        /// <summary>
        /// 添加新的控制变量
        /// </summary>
        /// <param name="parameter"></param>
        private void AddNewVariable(object parameter)
        {

            if (!IsVariableSelected)
            {
                SelectedVariable = new VariableItem()
                {
                    Name = "监控变量名",
                    Address = "0xFFFFFFFF",
                    Size = "0x1",
                    Offset = "0",
                    Type = "int8U"
                };
            }
            
            var dialog = new AddVariableDialog
            {

                Owner = Application.Current.MainWindow,
                DataContext = new AddVariableViewModel()
                {
                    VariableName = SelectedVariable.Name,
                    Address = SelectedVariable.Address,
                    Offset = SelectedVariable.Offset,
                    Size = SelectedVariable.Size,
                    Type = SelectedVariable.Type,
                }
            };

            if (dialog.ShowDialog() == true && dialog.DataContext is AddVariableViewModel vm)
            {
                var item = new VariableItem
                {
                    Name = vm.VariableName,
                    Address = vm.Address,
                    Offset = vm.Offset,
                    Size = vm.Size,
                    IsMonitored = vm.IsMonitored,
                    Type = vm.Type
                };
                lock (_syncLock)
                {
                    if (!_variablesDict.ContainsKey(item.Id))
                    {
                        _variablesDict[item.Id] = item;
                        Application.Current.Dispatcher.Invoke(() => Variables.Add(item));
                    }
                }

            }
        }

        /// <summary>
        /// 添加监控变量
        /// </summary>
        /// <param name="parameter"></param>
        private void AddVariable(object parameter)
        {
            if (HaveFindName(FindName))
            {
                string newName = FindName;//监控变量
                addressMap.TryGetValue(FindName, out string addre);//获取地址
                string path2 = "";
                if (addressMoveMap.TryGetValue(FindName, out string result))
                {
                    path2 = result;
                }
                else
                {
                    path2 = "未找到";
                }

                var item = new VariableItem
                {
                    Name = newName,
                    IsMonitored = true,
                    Address = addre,
                    Size = path2,
                    Type = SelectedType
                };
                lock (_syncLock)
                {
                    if (!_variablesDict.ContainsKey(item.Id))
                    {
                        _variablesDict[item.Id] = item;
                        Application.Current.Dispatcher.Invoke(() => Variables.Add(item));
                    }
                }

            }
            else
            {

            }
        }

        /// <summary>
        /// 删除监控变量
        /// </summary>
        /// <param name="parameter"></param>
        private void DeleteVariable(object parameter)
        {
            if (parameter is VariableItem item)
            {
                RemoveVariable(item.Id);
            }
        }


        /// <summary>
        /// 移除监控变量
        /// </summary>
        /// <param name="id"></param>
        public void RemoveVariable(Guid id)
        {
            lock (_syncLock)
            {
                if (_variablesDict.TryRemove(id, out var item))
                {
                    // 清理资源
                    item.Cleanup();

                    // 关闭关联的趋势窗口
                    if (_openTrendWindows.TryGetValue(id, out var window))
                    {
                        window.Close();
                        _openTrendWindows.Remove(id);
                    }

                    Application.Current.Dispatcher.Invoke(() => Variables.Remove(item));
                }
            }
        }

        // 添加对DataGrid的引用（在View中设置）
        public DataGrid VariablesGrid { get; set; }


        private bool CanDeleteVariable(object parameter) => IsVariableSelected;

        /// <summary>
        /// 显示趋势窗口
        /// </summary>
        /// <param name="parameter"></param>
        private void ShowTrend(object parameter)
        {
            //if (SelectedVariable == null) return;

            //// 创建趋势窗口
            //var trendWindow = new View.TrendWindow
            //{
            //    DataContext = new TrendViewModel(SelectedVariable.Name)
            //};
            //trendWindow.Show();

            //if (SelectedVariable!=null)
            //{
            //    // 检查是否已经打开过该变量的趋势窗口
            //    if (_openTrendWindows.TryGetValue(SelectedVariable.Id, out var existingWindow))
            //    {
            //        existingWindow.Activate();
            //        return;
            //    }

            //    // 创建趋势窗口
            //    var trendWindow = new NewTrendWindow
            //    {
            //        DataContext = SelectedVariable.TrendViewModel
            //    };

            //    // 监听窗口关闭事件
            //    trendWindow.Closed += (s, e) =>
            //    {
            //        _openTrendWindows.Remove(SelectedVariable.Id);
            //    };

            //    _openTrendWindows[SelectedVariable.Id] = trendWindow;
            //    trendWindow.Show();
            //}
            // 创建当前监控项的副本
            
            if (parameter is VariableItem item)
            {
                // 检查是否已经打开过该变量的趋势窗口
                if (_openTrendWindows.TryGetValue(item.Id, out var existingWindow))
                {
                    existingWindow.Activate();
                    return;
                }

                
                // 创建趋势窗口
                var trendWindow = new NewTrendWindow
                {
                    DataContext = item.TrendViewModel

                };
                item.TrendViewModel.VariableName = item.Name;
                // 监听窗口关闭事件
                trendWindow.Closed += (s, e) =>
                {
                    _openTrendWindows.Remove(item.Id);
                };

                _openTrendWindows[item.Id] = trendWindow;
                trendWindow.Show();
            }
        }

        private bool CanShowTrend(object parameter) => IsVariableSelected;

        /// <summary>
        /// 切换监控
        /// </summary>
        /// <param name="parameter"></param>
        private void ToggleMonitoring(object parameter)
        {

            lock (_syncLock)
            {
                if (parameter is VariableItem item)
                {
                    Application.Current.Dispatcher.Invoke(() => item.IsMonitored = !item.IsMonitored);
                }
            }
        }

        #endregion

        #region 其他
        /// <summary>
        /// 更新监控变量的数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateVariables(object sender, ElapsedEventArgs e)
        {
            // 更新监控中的变量值
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var variable in Variables)
                {
                    if (variable.IsMonitored)
                    {
                        variable.CurrentValue = _dataService.GetNextDataPoint();
                    }
                }
            });
        }


        public void Cleanup()
        {
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer.Dispose();
                _updateTimer = null;
            }
            // 清理所有变量资源
            foreach (var item in _variablesDict.Values)
            {
                item.Cleanup();
            }

            // 关闭所有趋势窗口
            foreach (var window in _openTrendWindows.Values)
            {
                window.Close();
            }
            _openTrendWindows.Clear();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RaiseProperChanged(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        #endregion

        #region 串口工具

        //串口实体类
        private SerialPortSettings serialPortSettings;

        /// <summary>
        /// 初始化串口工具
        /// </summary>
        private void IniCom()
        {
            //加载串口配置文件
            serialPortSettings = LoadCom();
            //初始化串口通讯工具
            SerialCommunicationService.InitiateCom(serialPortSettings);
        }

        #region 串口图标
        //串口打开图标
        private Visibility _ComIconOpen = Visibility.Visible;

        public Visibility ComIconOpen
        {
            get { return _ComIconOpen; }
            set
            {
                _ComIconOpen = value;
                this.RaiseProperChanged(nameof(ComIconOpen));
            }
        }

        //串口关闭图标
        private Visibility _ComIconClose = Visibility.Collapsed;

        public Visibility ComIconClose
        {
            get { return _ComIconClose; }
            set
            {
                _ComIconClose = value;
                this.RaiseProperChanged(nameof(ComIconClose));
            }
        }


        /// <summary>
        /// 改变串口打开图标
        /// </summary>
        /// <param name="flag"></param>
        private void ChangeComIcon(bool flag)
        {
            if (flag)
            {
                //串口打开
                ComIconClose = Visibility.Visible;
                ComIconOpen = Visibility.Collapsed;
            }
            else
            {
                //串口关闭
                ComIconClose = Visibility.Collapsed;
                ComIconOpen = Visibility.Visible;
            }
        }

        #endregion


        /// <summary>
        /// 从文件加载串口通讯信息
        /// </summary>
        /// <returns></returns>
        private SerialPortSettings LoadCom()
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
            }
            return new SerialPortSettings();
        }

        /// <summary>
        /// 打开串口(已打开则关闭串口)
        /// </summary>
        public void openCom(object obj)
        {
            if (SerialCommunicationService.IsOpen())
            {

                try
                {
                    //AddLog("准备关闭通信");
                    ////停止后台通信(如果有)
                    StopBackgroundThread();

                    //关闭串口
                    SerialCommunicationService.CloseCom();
                    //AddLog("串口已关闭");

                    ChangeComIcon(false);

                    UpdateState("串口已关闭");
                    comStateColor(false);
                    //AddLog($"关闭串口{SerialCommunicationService.getComName()}成功");
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                IniCom();
                if (!SerialCommunicationService.OpenCom())
                {
                    MessageBox.Show("串口打开失败！");
                    return;
                };
                ChangeComIcon(true);
                comStateColor(true);
                //AddLog($"打开串口{SerialCommunicationService.getComName()}成功");
                //MessageBox.Show("串口打开成功！");

                //开始通讯
                StartBackgroundThread();
            }

        }
        #endregion

        #region 后台通讯线程
        private CancellationTokenSource _cts = new CancellationTokenSource();//取消线程专用
        private ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);//暂停线程专用
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1); // 异步竞争


        // 后台线程是否正在运行
        private bool _isRunning = false;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                RaiseProperChanged("IsRunning");
            }
        }

        // 状态信息
        private string _status = "关闭";
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    RaiseProperChanged(nameof(Status));
                }

            }
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        /// <param name="state"></param>
        public void UpdateState(string state)
        {
            Status = state;
        }

        // 工作状态指示
        private bool _isWorking;
        public bool IsWorking
        {
            get => _isWorking;
            set
            {
                if (value == _isWorking) return;
                _isWorking = value;
                RaiseProperChanged(nameof(IsWorking));
            }
        }

        //状态灯
        private Brush comStatus = Brushes.Red;
        public Brush ComStatus
        {
            get
            {
                return comStatus;
            }
            set
            {
                comStatus = value;
                RaiseProperChanged(nameof(ComStatus));
            }
        }

        /// <summary>
        /// 设置状态灯颜色
        /// </summary>
        /// <param name="flag"></param>
        public void comStateColor(bool flag)
        {
            if (flag)
            {
                ComStatus = Brushes.Green;
            }
            else
            {
                ComStatus = Brushes.Red;
            }
        }

        // 命令定义
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ExecuteSpecialCommand { get; }


        /// <summary>
        /// 启动后台通信线程
        /// </summary>
        private void StartBackgroundThread()
        {

            if (!SerialCommunicationService.IsOpen())
            {
                MessageBox.Show("请先打开串口!", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (IsRunning) { return; }
            else
            {
                IsRunning = true;
                _cts = new CancellationTokenSource();
                _pauseEvent.Set();
                UpdateState("正在通信");
                comStateColor(true);
                Task.Run(() => BackgroundWorker(_cts.Token));

            }

        }

        /// <summary>
        /// 停止后台通信
        /// </summary>
        private void StopBackgroundThread()
        {
            _cts.Cancel();

        }

        /// <summary>
        /// 后台工作线程主循环
        /// </summary>
        private async Task BackgroundWorker(CancellationToken token)
        {
            int count = 0;
            string receive = "";
            string address;
            long result = 0;
            try
            {
                //COM通讯
                while (!token.IsCancellationRequested)
                {
                    // 创建当前监控项的副本
                    List<VariableItem> itemsToProcess;
                    lock (_syncLock)
                    {
                        itemsToProcess = _variablesDict.Values
                            .Where(v => v.IsMonitored)
                            .ToList();
                    }
                    foreach (VariableItem item in itemsToProcess)
                    {
                        if (token.IsCancellationRequested) break;
                        try
                        {
                            //计算地址 (初始地址+偏移量)
                            count = HesWithSeperateToInt(item.Address) + (int)ParseDecimalString(item.Offset);
                            address = GetPathFromHes(IntToHexWithSeparator(count));
                            //发送指令
                            if (item.Type == "int8U" || item.Type == "int8S")
                            {
                                receive = SerialCommunicationService.SendPathCommand($"TQFDEBUG{address}0", 7);
                                //解析返回
                                result = AnalysisReceiveInt8(receive);
                            }
                            else if (item.Type == "int16U" || item.Type == "int16S")
                            {
                                receive = SerialCommunicationService.SendPathCommand($"TQFDEBUG{address}1", 9);
                                //解析返回
                                result = AnalysisReceiveInt16(receive);
                            }
                            else
                            {
                                receive = SerialCommunicationService.SendPathCommand($"TQFDEBUG{address}2", 14);
                                //解析返回
                                result = AnalysisReceiveInt32(receive);
                            }
                            // 更新UI线程上的值
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // 再次检查变量是否仍然存在
                                if (_variablesDict.ContainsKey(item.Id))
                                {
                                    if (item.Type == "int8S")
                                    {
                                        result = 127 - result;
                                    }
                                    else if (item.Type == "int16S")
                                    {
                                        result = 32767 - result;
                                    }
                                    else if (item.Type == "int32S")
                                    {
                                        result = 2147483647 - result;
                                    }
                                    item.CurrentValue = result;
                                }
                            });

                        }
                        catch (Exception ex)
                        {
                            // 在UI线程显示错误
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"错误: {ex.Message}\n变量: {item.Name}");
                            });

                            break;
                        }



                    }
                    // 模拟常规通信
                    await Task.Delay(50, token);
                    //AddLog($"[后台] 常规通信: {DateTime.Now:HH:mm:ss.fff}");
                }
            }
            catch (OperationCanceledException)
            {

                IsRunning = false;
            }
            catch (Exception ex)
            {
                string mes = ex.ToString();

                UpdateState("异常!");
                //关闭串口
                openCom(new object());

                MessageBox.Show($"通讯异常{ex.ToString()},已停止通讯！,请重新打开串口", "异常", MessageBoxButton.OK, MessageBoxImage.Error);

            }
            finally
            {

                UpdateState("已停止通信!");
                IsRunning = false;
            }
        }

        #endregion

        #region 数据处理

        /// <summary>
        /// 解析返回数据(8位)
        /// </summary>
        /// <param name="receive"></param>
        /// <returns></returns>
        public long AnalysisReceiveInt8(string receive)
        {
            if (string.IsNullOrEmpty(receive))
            {
                //返回超时
                return -110;
            }
            if (receive.StartsWith("-1"))
            {
                //CRC校验不过
                return -111;
            }


            return ParseDecimalString(receive.Substring(1, 3));


        }

        /// <summary>
        /// 解析返回数据(16位)
        /// </summary>
        /// <param name="receive"></param>
        /// <returns></returns>
        public long AnalysisReceiveInt16(string receive)
        {
            if (string.IsNullOrEmpty(receive))
            {
                //返回超时
                return -110;
            }
            if (receive.StartsWith("-1"))
            {
                //CRC校验不过
                return -111;
            }


            return ParseDecimalString(receive.Substring(1, 5));


        }
        /// <summary>
        /// 解析返回数据(16位)
        /// </summary>
        /// <param name="receive"></param>
        /// <returns></returns>
        public long AnalysisReceiveInt32(string receive)
        {
            if (string.IsNullOrEmpty(receive))
            {
                //返回超时
                return -110;
            }
            if (receive.StartsWith("-1"))
            {
                //CRC校验不过
                return -111;
            }


            return ParseDecimalString(receive.Substring(1, 10));


        }

        /// <summary>
        /// 十进制转成8位十六进制
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public string IntToHexWithSeparator(int number)
        {
            // 转换为8位十六进制字符串（小写字母）
            string hex = number.ToString("x8");

            // 插入分隔符：前4位 + "'" + 后4位
            return $"0x{hex.Substring(0, 4)}'{hex.Substring(4)}";
        }

        /// <summary>
        /// 十六进制转十进制
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public int HesWithSeperateToInt(string hexString)
        {
            // 移除分隔符 ' 和前缀 0x
            string cleanHex = hexString.Replace("'", "").Replace("0x", "");

            // 转换为 int（指定基数为 16）
            int result = Convert.ToInt32(cleanHex, 16);

            return result;
        }

        /// <summary>
        /// 从地址获取
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public string GetPathFromHes(string hexString)
        {
            // 移除分隔符 ' 和前缀 0x
            string cleanHex = hexString.Replace("'", "").Replace("0x", "");
            return cleanHex;
        }

        /// <summary>
        /// 十进制字符串转数字
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="OverflowException"></exception>
        public long ParseDecimalString(string input)
        {
            // 1. 处理空值或空白字符串
            if (string.IsNullOrWhiteSpace(input))
                return 0;


            // 2. 移除可能存在的千位分隔符
            string cleanInput = input.Replace(",", "").Replace(".", "").Replace(" ", "").Replace("'", "");

            // 3. 处理正负号
            bool isNegative = false;
            if (cleanInput.StartsWith('-'))
            {
                isNegative = true;
                cleanInput = cleanInput.Substring(1);
            }
            else if (cleanInput.StartsWith('+'))
            {
                cleanInput = cleanInput.Substring(1);
            }

            // 4. 验证剩余字符是否都是数字
            foreach (char c in cleanInput)
            {
                if (!char.IsDigit(c))
                    throw new FormatException($"包含无效字符: '{c}'");
            }

            // 5. 检查是否为空（只有符号的情况）
            if (string.IsNullOrEmpty(cleanInput))
                throw new FormatException("缺少数字部分");

            // 6. 尝试解析
            if (int.TryParse(cleanInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return isNegative ? -result : result;
            }

            // 7. 处理超出范围的情况
            if (cleanInput.Length > 10 || (cleanInput.Length == 10 && cleanInput.CompareTo("2147483647") > 0))
            {
                if (long.TryParse(cleanInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result1))
                {
                    return isNegative ? -result1 : result1;
                }
            }

            throw new FormatException("无效的数字格式");
        }
        #endregion
    }

    /// <summary>
    /// 简单命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter) => _execute(parameter);
    }
}
