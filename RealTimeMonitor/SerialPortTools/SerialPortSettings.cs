using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace RealTimeMonitor.SerialPortTools
{
    [XmlRoot("SerialPortSettings")]
    public class SerialPortSettings
    {
        [XmlElement("PortName")]
        public string PortName { get; set; } = "COM1";

        [XmlElement("BaudRate")]
        public int BaudRate { get; set; } = 2400;

        [XmlElement("DataBits")]
        public int DataBits { get; set; } = 8;

        [XmlElement("StopBits")]
        public StopBits StopBits { get; set; } = StopBits.One;

        [XmlElement("Parity")]
        public Parity Parity { get; set; }=Parity.None;
    }
}

