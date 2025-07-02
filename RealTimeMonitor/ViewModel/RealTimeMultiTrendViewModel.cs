using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RealTimeMonitor.View;

namespace RealTimeMonitor.ViewModel
{
    public class RealTimeMultiTrendViewModel : INotifyPropertyChanged
    {

        //唯一表示
        public Guid Id { get; } = Guid.NewGuid();

        //发送指令地址
        private string address ="";

        //监控变量集合 
        private List<VariableItem> items = new List<VariableItem>();

        //实时曲线图像的DataContext
        public MultiTrendViewModel MultiTrendViewModel { get; set; }

        public RealTimeMultiTrendViewModel(List<VariableItem> variables)
        {
            //创建新的需要监控的集合
            foreach (var item in variables)
            {
                var itemViewModel = new VariableItem
                {
                    Name = item.Name,
                    Address = item.Address,
                    Size = item.Size,
                    Offset = item.Offset,
                    Type = item.Type,
                    CurrentValue = item.CurrentValue,
                };
                //把自带的自动刷新定时器停止
                itemViewModel.Cleanup();
                //添加到集合
                items.Add(itemViewModel);
                //计算初始地址+偏移量
                int count = HesWithSeperateToInt(itemViewModel.Address) + (int)ParseDecimalString(itemViewModel.Offset);
                //拼接地址 (地址 + 类型)
                address = address + GetPathFromHes(IntToHexWithSeparator(count)) + GetType(itemViewModel.Type);
            }

            //根据新的变量来创建ViewModel
            MultiTrendViewModel = new MultiTrendViewModel(items);
        }

        /// <summary>
        /// 解析收到的数据
        /// </summary>
        /// <param name="receive"></param>
        public void AnalyzeReceiveData(string receive)
        {
            if (String.IsNullOrEmpty(receive))
            {
                //接收为空
                ReceiveException(-110);
            }
            else if (receive.StartsWith("-1"))
            {
                //CRC校验没过
                ReceiveException(-111);
            }
            //进行分段
            string[] receives = receive.Substring(1, receive.Length - 3).Split(" ");

            //进行赋值
            if (receives.Length == items.Count)
            {
                //数据值的个数相等
                for (int i = 0; i < receives.Length; i++)
                {
                    long value = ParseDecimalString(receives[i]);
                    long result = 0;
                    if (items[i].Type == "int8S")
                    {
                        result = 127 - result;
                    }
                    else if (items[i].Type == "int16S")
                    {
                        result = 32767 - result;
                    }
                    else if (items[i].Type == "int32S")
                    {
                        result = 2147483647 - result;
                    }
                    items[i].CurrentValue = result;
                }
            }
            else
            {
                //解析异常

                ReceiveException(-10101);
            }
        }

        /// <summary>
        /// 获取需要发送的地址+类型
        /// </summary>
        /// <returns></returns>
        public string GetSendCommand()
        {
            return address;
        }


        /// <summary>
        /// 获取相应类型的指令值
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string GetType(string type)
        {
            if (!string.IsNullOrEmpty(type))
            {
                if (type == "int8U" || type == "int8S")
                {
                    return "0";
                }
                else if (type == "int16U" || type == "int16S")
                {
                    return "1";
                }
                else return "2";
            }
            else
            {
                return "0";
            }
        }
        /// <summary>
        /// 接收数据异常赋值
        /// </summary>
        /// <param name="value"></param>
        private void ReceiveException(double value)
        {
            foreach (var item in items)
            {
                item.CurrentValue = value;
            }
        }

        #region 数据转换方法

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
        /// 获取格式化地址地址
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
