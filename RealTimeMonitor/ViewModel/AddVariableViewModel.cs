using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace RealTimeMonitor.ViewModel
{
    public class AddVariableViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private string _variableName;
        private string _address;
        private string _size;
        private string _type;
        private string _offset;
        private bool _isMonitored = true;
        private bool _isValid;

        public ObservableCollection<string> Types { get; set; } = new ObservableCollection<string>() { "int8U", "int16U", "int32U", "int8S", "int16S", "int32S" };
        public string VariableName
        {
            get => _variableName;
            set
            {
                _variableName = value;
                OnPropertyChanged(nameof(VariableName));
                Validate();
            }
        }

        public string Address
        {
            get => _address;
            set
            {
                _address = value;
                OnPropertyChanged(nameof(Address));
                Validate();
            }
        }
        public string Size
        {
            get => _size;
            set
            {
                _size = value;
                OnPropertyChanged(nameof(Size));
                Validate();
            }
        }

        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
                Validate();
            }
        }

        public string Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                OnPropertyChanged(nameof(Offset));
                Validate();
            }
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

        public bool IsValid
        {
            get => _isValid;
            set
            {
                _isValid = value;
                OnPropertyChanged(nameof(IsValid));
            }
        }

        public Action CloseAction { get; set; }
        public ICommand ConfirmCommand { get; }

        public AddVariableViewModel()
        {
            ConfirmCommand = new RelayCommand(Confirm);
        }

        private void Confirm(object parameter)
        {
            if (IsValid)
            {
                CloseAction?.Invoke();
            }
        }

        private void Validate()
        {
            IsValid = !string.IsNullOrWhiteSpace(VariableName) &&
                      !string.IsNullOrWhiteSpace(Address) &&
                       !string.IsNullOrWhiteSpace(Offset)&&
                       !string.IsNullOrWhiteSpace(Size) ;
        }

        #region IDataErrorInfo Implementation
        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                string error = null;
                switch (columnName)
                {
                    case nameof(VariableName):
                        if (string.IsNullOrWhiteSpace(VariableName))
                            error = "变量名称不能为空";
                        break;

                    case nameof(Address):
                        if (string.IsNullOrWhiteSpace(Address))
                            error = "地址不能为空";
                        break;

                    case nameof(Offset):
                        if (string.IsNullOrWhiteSpace(Offset))
                            error = "偏移量必须在0-100之间";
                        break;
                }
                return error;
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
