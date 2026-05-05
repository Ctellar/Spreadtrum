using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spreadtrum.Library
{
    public class spdio_t
    {
        private const int RECV_BUF_LEN = 0x8000;

        public byte[] big_buf = new byte[0];

        public int recv_off, raw_off, temp_off, untrans_off, enc_off;
        public int recv_len, raw_len, enc_len;

        public ArraySegment<byte> recv_buf;
        public ArraySegment<byte> raw_buf;
        public ArraySegment<byte> temp_buf;
        public ArraySegment<byte> untranscode_buf;
        public ArraySegment<byte> enc_buf;
        public ArraySegment<byte> send_buf;

        public int flags, recv_pos;
        public int verbose, timeout, part_count;
        public IntPtr handle, m_hRecvThreadState, m_hRecvThread;
        public uint m_dwRecvThreadID;
        public AutoResetEvent? m_hOprEvent;
        public partition_t[] ptable = new partition_t[128];

        public static spdio_t spdio_init(int flags)
        {
            var io = new spdio_t();

            int total_size = RECV_BUF_LEN + (4 + 0x10000 + 2) * 4 + 4;
            io.big_buf = new byte[total_size];

            int p = 0;

            io.recv_off = p;
            io.recv_len = RECV_BUF_LEN;
            io.recv_buf = new ArraySegment<byte>(io.big_buf, io.recv_off, io.recv_len);
            p += RECV_BUF_LEN;

            io.raw_off = p;
            io.raw_len = 4 + 0x10000 + 2;
            io.raw_buf = new ArraySegment<byte>(io.big_buf, io.raw_off, io.raw_len);
            p += io.raw_len;

            io.temp_off = io.raw_off + 5;
            io.temp_buf = new ArraySegment<byte>(io.big_buf, io.temp_off, io.raw_len - 5);

            io.untrans_off = p;
            int untrans_len = 4 + 0x10000 + 4;
            io.untranscode_buf = new ArraySegment<byte>(io.big_buf, io.untrans_off, untrans_len);
            p += untrans_len;

            io.enc_off = p;
            io.enc_len = total_size - p;
            io.enc_buf = new ArraySegment<byte>(io.big_buf, io.enc_off, io.enc_len);

            io.flags = flags;
            io.timeout = 1000;
            io.m_hOprEvent = new AutoResetEvent(false);

            Array.Clear(io.big_buf, io.recv_off, 8);

            return io;
        }
    }

    internal class spd_channel
    {
        private static SerialPort? _serialPort;

        public static bool call_ConnectChannel(string portName, int baudRate = 115200)
        {
            try
            {
                _serialPort = new SerialPort(portName, baudRate)
                {
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 5000,
                    WriteTimeout = 5000
                };

                _serialPort.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void call_SetProperty(int baudRate)
        {
            if (_serialPort != null)
            {
                _serialPort.BaudRate = baudRate;
            }
        }

        public static void call_Uninitialize()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();

                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        public bool IsConnected => _serialPort?.IsOpen == true;

        public static int call_Write(byte[] buffer, int offset, int size)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                return -1;

            try
            {
                _serialPort.Write(buffer, offset, size);
                return size;
            }
            catch
            {
                return -1;
            }
        }

        public static int call_Read(byte[] buffer, int offset, int maxLen, int timeout)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                return -1;

            try
            {
                _serialPort.ReadTimeout = timeout;

                int bytesRead = _serialPort.Read(buffer, offset, maxLen);
                return bytesRead;
            }
            catch (TimeoutException)
            {
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        public static void call_Clear()
        {
            if (_serialPort == null) return;

            if (_serialPort.IsOpen)
            {
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
            }
        }
    }
}
