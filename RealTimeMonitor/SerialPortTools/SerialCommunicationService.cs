using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RealTimeMonitor.SerialPortTools
{
    public class SerialCommunicationService
    {
        //串口
        static SerialPort SerialPort { get; set; }

        //串口类
        static SerialPortSettings SerialPortModel { get; set; }

        //串口发送帧数
        public static Action<int> AddSendFrame;
        //串口接收帧数
        public static Action<int> AddReceiveFrame;
        //设置CRC抗干扰开关
        public static Action<bool> ReciveCRCSwitch;
        //CRC校验
        private static bool Receive_CRC_Check = false;
        //校验抗干扰校验开启
        private static int IsReceiveCRC_OPen = 0;
        //校验抗干扰校验关闭
        private static int IsReceiveCRC_CLose = 0;
        // 异步竞争
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        //机器类型
        public static string _MachineType = "";
        public static string MachineType
        {
            get { return _MachineType; }
            set
            {
                if (value.Length >= 9)
                {
                    value = value.Substring(0, 9);
                    if (value == "(HPVINV01") _MachineType = "A";
                    else if (value == "(HPVINV02" || value == "(HPVINV05") _MachineType = "B";
                    else if (value == "(HPVINV03") _MachineType = "C";
                    else if (value == "(HPVINV04") _MachineType = "D";
                }
                else
                    _MachineType = value;
            }
        }


        /// <summary>
        /// 初始化
        /// </summary>
        public static void InitiateCom(SerialPortSettings serialPortSettings)
        {
            SerialPortModel = serialPortSettings;
            SerialPort = new SerialPort();
            SerialPort.PortName = SerialPortModel.PortName;
            SerialPort.Parity = SerialPortModel.Parity;
            SerialPort.StopBits = SerialPortModel.StopBits;
            SerialPort.DataBits = SerialPortModel.DataBits;
            SerialPort.BaudRate = SerialPortModel.BaudRate;
        }

        /// <summary>
        /// 判断串口是否打开
        /// </summary>
        /// <returns></returns>
        public static bool IsOpen()
        {
            return SerialPort.IsOpen;
        }

        /// <summary>
        /// 获取串口名字
        /// </summary>
        /// <returns></returns>
        public static string getComName()
        {
            return SerialPort.PortName;
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        /// <returns></returns>
        public static bool CloseCom()
        {
            if (SerialPort.IsOpen)
            {
                try
                {
                    SerialPort.Close();
                }
                catch (Exception ex)
                {
                    SerialPort.Close();
                    return false;
                }
                return true;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        /// <returns></returns>
        public static bool OpenCom()
        {
            if (!SerialPort.IsOpen)
            {
                try
                {

                    SerialPort.Open();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else
            {
                CloseCom();
                try
                {
                    SerialPort.Open();
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 打开或者关闭接收校验
        /// </summary>
        public static void OpenReceiveCRC(Object parameter)
        {
            if (parameter is bool isChecked)
            {
                // 根据 ToggleButton 的选中状态执行相应逻辑
                if (isChecked)
                {
                    // 处理选中状态
                    Receive_CRC_Check = true;
                }
                else
                {
                    // 处理未选中状态
                    Receive_CRC_Check = false;
                }
            }

        }

        /// <summary>
        /// 根据实际接收字节情况自动判断是否处于抗干扰模式
        /// </summary>
        /// <param name="DestinationReceive"></param>
        /// <param name="ActualReceive"></param>
        private static void ChangeCRCR_Receive(int DestinationReceive, int ActualReceive)
        {
            //第一种情况(已开启抗干扰但上位机不知道
            if (DestinationReceive < ActualReceive)
            {
                int num = ActualReceive - DestinationReceive;
                if (num == 2)
                {
                    IsReceiveCRC_OPen++;
                    if (IsReceiveCRC_OPen == 10)
                    {
                        ReciveCRCSwitch(true);
                        OpenReceiveCRC(true);
                        IsReceiveCRC_OPen = 0;
                    }
                }
            }//
            //第二种情况（已关闭抗干扰但上位机不知道
            else if (DestinationReceive > ActualReceive)
            {
                int num = DestinationReceive - ActualReceive;
                if (num == 2)
                {
                    IsReceiveCRC_CLose++;
                    if (IsReceiveCRC_CLose == 10)
                    {
                        ReciveCRCSwitch(false);
                        OpenReceiveCRC(false);
                        IsReceiveCRC_CLose = 0;
                    }
                }
            }

        }

        /// <summary>
        /// 确保遗留数据全部丢弃
        /// </summary>
        static void SafeDiscardInBuffer()
        {
            SerialPort.DiscardInBuffer();
            while (SerialPort.BytesToRead > 0)
            {
                Thread.Sleep(10); // 短暂等待
                SerialPort.DiscardInBuffer(); // 再次丢弃
            }
        }


        /// <summary>
        /// 发送指令
        /// </summary>
        /// <param name="command">字符串指令</param>
        /// <param name="returnCount">返回字节数</param>
        /// <returns></returns>
        public static string SendCommand(string command, int returnCount)
        {
            _semaphore.Wait();

            SerialPort.WriteTimeout = 1000;
            //写命令
            byte[] Command = Encoding.ASCII.GetBytes(command);
            //接收帧数
            int totalBytesRead = 0;
            //判断是否需要接收校验CRC
            if (Receive_CRC_Check)
            {
                //添加两个校验字节
                returnCount += 2;
            }
            //在写命令之前先清空一下接受缓存
            SerialPort.DiscardInBuffer();
            //收报文
            try
            {
                SerialPort.Write(Command, 0, Command.Length);
                //增加延时100ms
                Thread.Sleep(100);
                //添加发送帧数
                AddSendFrame(Command.Length);
                // 设置读取超时时间【1s】
                SerialPort.ReadTimeout = 1000;
                // 需要读取的字节数
                int bytesToRead = returnCount;
                //读取输入缓冲区
                byte[] buffer = new byte[bytesToRead];
                totalBytesRead = 0;
                //设置读取超时，1s内达不到所需字节就触发超时异常
                while (totalBytesRead < bytesToRead)
                {
                    int bytesRead = SerialPort.Read(buffer, totalBytesRead, bytesToRead - totalBytesRead);
                    totalBytesRead += bytesRead;
                }

                //增加接收返回帧数
                AddReceiveFrame(totalBytesRead);

                //对返回字节进行CRC校验
                if (Receive_CRC_Check && buffer.Length == returnCount)
                {
                    byte[] origin = buffer;
                    byte[] crcori;
                    byte[] build;
                    bool CRC_Pass = CheckReceive_CRC(buffer, out crcori, out build);
                    if (!CRC_Pass)
                    {
                        //CRC校验不通过

                        return "-1   " + Encoding.ASCII.GetString(origin) + $"收到的CRC:{crcori[0]},{crcori[1]};校验值:{build[0]},{build[1]}";
                    }
                }

                //获取返回转字符串
                string DataBuffer = Encoding.ASCII.GetString(buffer);
                return DataBuffer;
            }
            catch (Exception ex)
            {
                // 超时未
                //MessageBox.Show("超时未收到ACK");
                //增加接收返回帧数
                AddReceiveFrame(totalBytesRead);
                return string.Empty;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 字节数组转换为字符串
        /// </summary>
        /// <param name="buff"></param>
        /// <returns></returns>
        private static string BytesToString(byte[] buff)
        {

            string result = "{";
            for (int i = 0; i < buff.Length; i++)
            {
                result = result + buff[i] + ",";
            }
            result += "}";
            return result;
        }

        public static void DiscardInBuff()
        {
            //在写命令之前先清空一下接受缓存
            SerialPort.DiscardInBuffer();
        }

        /// <summary>
        /// 发送指令(发送设置指令时用到)
        /// </summary>
        /// <param name="command">字节指令</param>
        /// <param name="returnCount">返回字节数</param>
        /// <returns></returns>
        public static string SendCommand(byte[] command, int returnCount)
        {
            _semaphore.Wait();
            int totalBytesRead = 0;

            //收报文
            try
            {
                //在写命令之前先清空一下接受缓存
                SerialPort.DiscardInBuffer();
                SerialPort.WriteTimeout = 1000;
                //写命令
                byte[] Command = command;
                SerialPort.Write(Command, 0, Command.Length);
                //添加发送帧数
                //AddSendFrame(Command.Length);
                // 设置读取超时时间【1s】
                SerialPort.ReadTimeout = 1000;
                // 需要读取的字节数
                int bytesToRead = returnCount;
                //读取输入缓冲区
                byte[] buffer = new byte[bytesToRead];
                totalBytesRead = 0;
                //设置读取超时，1s内达不到所需字节就触发超时异常
                while (totalBytesRead < bytesToRead)
                {
                    int bytesRead = SerialPort.Read(buffer, totalBytesRead, bytesToRead - totalBytesRead);
                    totalBytesRead += bytesRead;
                }
                //增加接收返回帧数
                //AddReceiveFrame(totalBytesRead);
                //对返回字节进行CRC校验
                if (Receive_CRC_Check && buffer.Length == returnCount)
                {
                    byte[] origin = buffer;
                    byte[] crcori;
                    byte[] build;
                    bool CRC_Pass = CheckReceive_CRC(buffer, out crcori, out build);
                    if (!CRC_Pass)
                    {
                        //CRC校验不通过

                        return "-1   " + Encoding.ASCII.GetString(origin) + $"收到的CRC:{crcori[0]},{crcori[1]};校验值:{build[0]},{build[1]}";
                    }
                }

                //获取返回转字符串
                string DataBuffer = Encoding.ASCII.GetString(buffer);
                return DataBuffer;
            }
            catch (TimeoutException ex)
            {
                // 超时未
                //MessageBox.Show("超时未收到ACK");
                //增加接收返回帧数
                //AddReceiveFrame(totalBytesRead);
                return string.Empty;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// 发送地址查询指令
        /// </summary>
        /// <param name="frontCommand"></param>
        /// <param name="setValue"></param>
        /// <returns></returns>
        public static string SendPathCommand(string frontCommand, int returnNum)
        {
            //指令与设置值结合
            string Command = frontCommand;
            //转成字节
            byte[] bytes = Encoding.ASCII.GetBytes(Command);
            //获取CRC校验字节
            byte[] CRC = getCRC(bytes);
            //拼接
            byte[] buffer = bytes.Concat(CRC).ToArray();
            //末尾的字节
            byte[] endCommand = Encoding.ASCII.GetBytes("\r");
            //完成指令
            byte[] sendCommand = buffer.Concat(endCommand).ToArray();
            //发送指令
            string receive = SendCommand(sendCommand, returnNum);
            return receive;
        }


        /// <summary>
        /// 发送设置指令
        /// </summary>
        /// <param name="frontCommand"></param>
        /// <param name="setValue"></param>
        /// <returns></returns>
        public static string SendSettingCommand(string frontCommand, string setValue)
        {
            //指令与设置值结合
            string Command = frontCommand + setValue;
            //转成字节
            byte[] bytes = Encoding.ASCII.GetBytes(Command);
            //获取CRC校验字节
            byte[] CRC = getCRC(bytes);
            //拼接
            byte[] buffer = bytes.Concat(CRC).ToArray();
            //末尾的字节
            byte[] endCommand = Encoding.ASCII.GetBytes("\r");
            //完成指令
            byte[] sendCommand = buffer.Concat(endCommand).ToArray();
            //发送指令
            string receive = SendCommand(sendCommand, 7);
            return receive;
        }

        /// <summary>
        /// CRC校验
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static bool CheckReceive_CRC(byte[] bytes, out byte[] crcOri, out byte[] crcGet)
        {
            //if (bytes == null) return false;
            //int CRC_length = bytes.Length - 3;
            ////创建新的字节数组进行CRC校验
            //byte[] buffer = new byte[CRC_length];
            //byte[] CRC_Receuve = new byte[2];
            //Array.Copy(bytes, buffer,CRC_length);
            //Array.Copy(CRC_Receuve, CRC_length, bytes,0,2);
            ////获取CRC校验码
            //byte[] CRC_Build = getCRC(buffer);
            ////判断两个校验码是否一致
            //bool isEuqal = CRC_Build.SequenceEqual(CRC_Receuve);
            //return isEuqal;
            crcOri = new byte[2];
            crcGet = new byte[2];
            if (bytes == null) return false;
            int CRC_length = bytes.Length - 3;
            // 创建新的字节数组进行 CRC 校验
            byte[] buffer = new byte[CRC_length];
            byte[] CRC_Receuve = new byte[2];

            // 复制除 CRC 校验码外的数据到 buffer 数组
            Array.Copy(bytes, 0, buffer, 0, CRC_length);

            // 从 bytes 数组中提取接收到的 CRC 校验码到 CRC_Receuve 数组
            Array.Copy(bytes, CRC_length, CRC_Receuve, 0, 2);

            // 获取 CRC 校验码
            byte[] CRC_Build = getCRC(buffer);
            crcOri = CRC_Receuve;
            crcGet = CRC_Build;
            // 判断两个校验码是否一致
            bool isEqual = CRC_Build.SequenceEqual(CRC_Receuve);
            return isEqual;
        }


        

        #region CRC校验

        /**************************************校验*************************************************************/
        /// <summary>
        /// 校验和
        /// </summary>
        /// <param name="checkCode">需要校验的字节数组</param>
        /// <param name="len">数组长度</param>
        /// <returns>字节数组的校验和</returns>
        public static int Check_Sum_Operation(byte[] checkCode, int len)
        {
            int checkSumResult = 0;
            int checkSum = 0;
            int i = 0;

            for (i = 0; i < len; i++)
            {
                checkSum += checkCode[i];
            }
            checkSumResult = checkSum & 0xFFFF;
            return checkSumResult;
        }


        /// <summary>
        /// 获取CRC校验码
        /// </summary>
        /// <param name="data"></param>
        /// <returns>字节数组，含两个字节</returns>
        public static byte[] getCRC(byte[] data)
        {
            byte[] CRC = new byte[2];
            int value = xmodem_crc16_ccitt(data);//从数据包中获取校验码
            CRC[0] = U16_MSB(value);//获取高位校验码
            CRC[1] = U16_LSB(value);//获取地位校验码
            return CRC;
        }
        //RTU_CRC
        static int RTU_CalCRC16(byte[] pucFrame, int usDataLen)
        {
            byte ucCRCHi = 0xFF;     // 高CRC字节初始化
            byte ucCRCLo = 0xFF;     // 低CRC字节初始化
            int wIndex = 0;          // CRC循环中的索引
            int i = 0;

            while (usDataLen > 0)
            {
                usDataLen--;
                wIndex = ucCRCLo ^ (pucFrame[i]);
                ucCRCLo = (byte)(ucCRCHi ^ rtu_aucCRCHi[wIndex]);
                ucCRCHi = rtu_aucCRCLo[wIndex];
                i++;
            }

            return (int)(ucCRCHi << 8 | ucCRCLo);
        }

        // CRC16 高位字节值表
        public static byte[] rtu_aucCRCHi = new byte[256]
        {
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40
        };

        // CRC16 低位字节值表
        public static byte[] rtu_aucCRCLo = new byte[256]
        {
            0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7,
            0x05, 0xC5, 0xC4, 0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E,
            0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09, 0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9,
            0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD, 0x1D, 0x1C, 0xDC,
            0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
            0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32,
            0x36, 0xF6, 0xF7, 0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D,
            0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A, 0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38,
            0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE, 0x2E, 0x2F, 0xEF,
            0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
            0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1,
            0x63, 0xA3, 0xA2, 0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4,
            0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F, 0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB,
            0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB, 0x7B, 0x7A, 0xBA,
            0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
            0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0,
            0x50, 0x90, 0x91, 0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97,
            0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C, 0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E,
            0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88, 0x48, 0x49, 0x89,
            0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
            0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83,
            0x41, 0x81, 0x80, 0x40
        };

        private static byte U16_MSB(int data)
        {
            return (byte)(data >> 8);
        }

        static private byte U16_LSB(int data)
        {
            return (byte)(data & 0xff);
        }
        //TQF_CRC
        static private int[] crc_ta = new int[16]
        {
                0x0000,0x1021,0x2042,0x3063,0x4084,0x50a5,0x60c6,0x70e7,

                0x8108,0x9129,0xa14a,0xb16b,0xc18c,0xd1ad,0xe1ce,0xf1ef
        };

        static private int cal_crc_half(byte[] pin, int len)
        {
            int i = 0;
            int crc;

            byte da;
            byte[] ptr = new byte[len];
            byte bCRCHign;
            byte bCRCLow;

            for (i = 0; i < ptr.Length; i++)
            {
                ptr[i] = pin[i];
            }

            crc = 0;
            i = 0;

            while (len-- != 0)
            {
                da = (byte)(((byte)(crc >> 8)) >> 4); /* 暂存CRC的高四位 */

                crc <<= 4; /* CRC右移4位，相当于取CRC的低12位）*/

                crc ^= crc_ta[(da ^ (ptr[i] >> 4))]; /* CRC的高4位和本字节的前半字节相加后查表计算CRC，然后加上上一次CRC的余数 */

                da = (byte)(((byte)(crc >> 8)) >> 4); /* 暂存CRC的高4位 */

                crc <<= 4; /* CRC右移4位， 相当于CRC的低12位） */

                crc ^= crc_ta[(da ^ (ptr[i] & 0x0f))]; /* CRC的高4位和本字节的后半字节相加后查表计算CRC，然后再加上上一次CRC的余数 */

                i++;
            }

            bCRCLow = (byte)crc;

            bCRCHign = (byte)(crc >> 8);

            if (bCRCLow == 0x28 || bCRCLow == 0x0d || bCRCLow == 0x0a)
            {
                bCRCLow++;
            }
            if (bCRCHign == 0x28 || bCRCHign == 0x0d || bCRCHign == 0x0a)
            {
                bCRCHign++;
            }
            crc = (int)bCRCHign << 8;
            crc += bCRCLow;
            return (crc);
        }
        //MODE1K_CRC
        static int xmodem_crc16_ccitt(byte[] pbBuf)
        {
            int wCRC = 0;
            int i = 0;

            byte bCycle;
            int dwLen = pbBuf.Length;
            while (dwLen-- > 0)
            {
                wCRC ^= (pbBuf[i] << 8);
                i++;
                for (bCycle = 0; bCycle < 8; bCycle++)
                {
                    if ((wCRC & 0x8000) != 0)
                    {
                        wCRC = ((wCRC << 1) ^ 0x1021);
                    }
                    else
                    {
                        wCRC <<= 1;
                    }
                }
            }
            return wCRC;
        }

        /// <summary>
        /// CRC16校验（校验码自动赋值在传参的最后两位）
        /// </summary>
        /// <param name="data">参与计算字节</param>
        /// <returns></returns>
        public static byte[] CRC16(byte[] arr)
        {
            //byte[] res = new byte[2];                         
            //byte[] crcbuf = data.ToArray();
            ////计算并填写CRC校验码
            //int crc = 0xffff;
            //int len = crcbuf.Length;
            //for (int n = 0; n < len; n++)
            //{
            //    byte i;
            //    crc = crc ^ crcbuf[n];
            //    for (i = 0; i < 8; i++)
            //    {
            //        int TT;
            //        TT = crc & 1;
            //        crc = crc >> 1;
            //        crc = crc & 0x7fff;
            //        if (TT == 1)
            //        {
            //            crc = crc ^ 0xa001;
            //        }
            //        crc = crc & 0xffff;
            //    }

            //}
            //res[1] = (byte)((crc >> 8) & 0xff);
            //res[0] = (byte)((crc & 0xff));
            //ucCRCHi = res[1];
            //ucCRCLo = res[0];
            //return res;

            ushort CRCReg = 0xFFFF;//定义crc寄存器
            for (int i = 0; i < arr.Length - 2; i++)
            {
                CRCReg ^= arr[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((CRCReg & 1) == 0)
                    {
                        CRCReg >>= 1;
                    }
                    else
                    {
                        CRCReg >>= 1;
                        CRCReg ^= 0xA001;
                    }
                }
            }
            arr[arr.Length - 2] = (byte)(CRCReg & 0x00FF);//低字节在前
            //ucCRCHi = arr[arr.Length - 2];
            arr[arr.Length - 1] = (byte)(CRCReg >> 8);//高字节在后
            //ucCRCLo = arr[arr.Length - 1];
            return arr;
        }
        #endregion
    }
}
