using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RealTimeMonitor.ViewModel
{
    /// <summary>
    /// 监控变量模型
    /// </summary>
    public class VariableItem : INotifyPropertyChanged
    {
        private string _name;               //监控变量名
        private string _address;            //地址
        private string _size;               //大小
        private string _type;               //数据类型
        private string _offset;             //偏移量
        private double _currentValue;       //当前值
        private bool _isMonitored;          //是否正在监控


        // 每个变量关联的趋势视图模型
        public NewTrendViewModel TrendViewModel { get; set; }

        public VariableItem()
        {
            // 初始化趋势视图模型
            TrendViewModel = new NewTrendViewModel(Name);
        }

        // 添加唯一标识符
        public Guid Id { get; } = Guid.NewGuid();
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Address
        {
            get => _address;
            set
            {
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged(nameof(Address));
                }
            }
        }

        public string Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }

        //偏移量
        public string Offset
        {
            get => _offset;
            set
            {
                if (_offset != value)
                {
                    _offset = value;
                    OnPropertyChanged(nameof(Offset));
                }
            }
        }

        public string Type
        {
            get => _type;
            set
            {
                if (value != _type)
                {
                    _type = value;
                    OnPropertyChanged(nameof(Type));
                }
            }
        }

        public double CurrentValue
        {
            get => _currentValue;
            set
            {
                _currentValue = value;
                OnPropertyChanged(nameof(CurrentValue));
                // 通知趋势视图添加新数据点
                TrendViewModel?.AddDataPoint(value);
            }
        }

        // 清理资源
        public void Cleanup()
        {
            TrendViewModel?.Cleanup();
        }

        public bool IsMonitored
        {
            get => _isMonitored;
            set
            {
                _isMonitored = value;
                OnPropertyChanged(nameof(IsMonitored));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
