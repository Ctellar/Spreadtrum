using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreadtrum
{
    public partial class Device
    {
        public string? Name { get; set; }
        public string? Hwid { get; set; }
        public string? COM { get; set; }
    }

    internal static class UsbWatcher
    {
        private static List<Device> lastDevices = new List<Device>();
        private static Timer? pollTimer;

        private static Guid GUID_DEVINTERFACE_COMPORT = new Guid("4d36e978-e325-11ce-bfc1-08002be10318");

        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        private const uint SPDRP_HARDWAREID = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid ClassGuid,
            IntPtr Enumerator,
            IntPtr hwndParent,
            uint Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet,
            uint MemberIndex,
            ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr DeviceInfoSet,
            ref SP_DEVINFO_DATA DeviceInfoData,
            uint Property,
            out uint PropertyRegDataType,
            byte[] PropertyBuffer,
            uint PropertyBufferSize,
            out uint RequiredSize);

        public static void StartPolling(int intervalMs = 500)
        {
            pollTimer = new Timer(_ =>
            {
                var current = GetDevices();
                DetectChanges(lastDevices, current);
                lastDevices = current;
            }, null, 0, intervalMs);
        }

        public static List<Device> GetDevices()
        {
            var devices = new List<Device>();
            IntPtr hDevInfo = SetupDiGetClassDevs(ref GUID_DEVINTERFACE_COMPORT, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
            if (hDevInfo == IntPtr.Zero) return devices;

            var devInfoData = new SP_DEVINFO_DATA();
            devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

            uint index = 0;
            while (SetupDiEnumDeviceInfo(hDevInfo, index, ref devInfoData))
            {
                index++;
                string? friendlyName = GetProperty(hDevInfo, devInfoData, SPDRP_FRIENDLYNAME);
                string? hwid = GetProperty(hDevInfo, devInfoData, SPDRP_HARDWAREID);

                if (!string.IsNullOrEmpty(friendlyName))
                {
                    string? comPort = ExtractComPort(friendlyName);
                    devices.Add(new Device
                    {
                        Name = friendlyName,
                        Hwid = hwid,
                        COM = comPort
                    });
                }
            }
            return devices;
        }

        public static List<Device> GetDevices(string devName)
        {
            var devices = new List<Device>();
            IntPtr hDevInfo = SetupDiGetClassDevs(ref GUID_DEVINTERFACE_COMPORT, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
            if (hDevInfo == IntPtr.Zero) return devices;

            var devInfoData = new SP_DEVINFO_DATA();
            devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

            uint index = 0;
            while (SetupDiEnumDeviceInfo(hDevInfo, index, ref devInfoData))
            {
                index++;
                string? friendlyName = GetProperty(hDevInfo, devInfoData, SPDRP_FRIENDLYNAME);
                string? hwid = GetProperty(hDevInfo, devInfoData, SPDRP_HARDWAREID);

                if (!string.IsNullOrEmpty(friendlyName))
                {
                    string? comPort = ExtractComPort(friendlyName);
                    devices.Add(new Device
                    {
                        Name = friendlyName,
                        Hwid = hwid,
                        COM = comPort
                    });
                }
            }
            return devices;
        }

        private static string? GetProperty(IntPtr hDevInfo, SP_DEVINFO_DATA devInfoData, uint property)
        {
            byte[] buffer = new byte[1024];
            uint regType, requiredSize;
            if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfoData, property, out regType, buffer, (uint)buffer.Length, out requiredSize))
            {
                return Encoding.Unicode.GetString(buffer, 0, (int)requiredSize - 2);
            }
            return null;
        }

        private static string? ExtractComPort(string friendlyName)
        {
            int start = friendlyName.IndexOf("(COM");
            if (start == -1) return null;
            int end = friendlyName.IndexOf(")", start);
            if (end == -1) return null;
            return friendlyName.Substring(start + 1, end - start - 1);
        }

        private static void DetectChanges(List<Device> oldList, List<Device> newList)
        {
            if (oldList.Count == newList.Count)
                return;
        }

        public static async Task<Device> FindDeviceByNameAsync(string devName, int pTimeout)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                if (stopwatch.ElapsedMilliseconds >= pTimeout) break;
                foreach (var device in GetDevices(devName))
                {
                    if (device.Name?.ToUpper().Contains(devName.ToUpper()) == true)
                    {
                        return device;
                    }
                }
                await Task.Delay(1000);
            }
            throw new TimeoutException("searching Timeout.");
        }

        public static async Task<Device> FindDeviceByPidVidAsync(string devName, int pTimeout)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                if (stopwatch.ElapsedMilliseconds >= pTimeout) break;
                foreach (var device in GetDevices(devName))
                {
                    if (device.Hwid?.ToUpper().Contains(devName.ToUpper()) == true)
                    {
                        return device;
                    }
                }
                await Task.Delay(1000);
            }
            throw new TimeoutException("searching Timeout.");
        }

        public static Device FindDeviceByName(string devName, int pTimeout)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                if (stopwatch.ElapsedMilliseconds >= pTimeout) break;
                foreach (var device in GetDevices(devName))
                {
                    if (device.Name?.ToUpper().Contains(devName.ToUpper()) == true)
                    {
                        return device;
                    }
                }
                Thread.Sleep(1000);
            }
            throw new TimeoutException("searching Timeout.");
        }
    }
}
