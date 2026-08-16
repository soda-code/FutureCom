using System;
using System.IO.Ports;

namespace FutureCom.Services
{
    public class SerialService : IDisposable
    {
        private SerialPort? _port;

        public bool IsOpen => _port?.IsOpen ?? false;

        /// <summary>
        /// 打开串口，支持完整的物理层参数配置（波特率、校验位、数据位、停止位）
        /// </summary>
        public void Open(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            Close();
            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            _port.Open();
        }

        public void Close()
        {
            if (_port != null)
            {
                if (_port.IsOpen)
                {
                    _port.Close();
                }
                _port.Dispose();
                _port = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}