using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static Spreadtrum.Library.spd_cons;

namespace Spreadtrum.Library
{
    internal class spd_field
    {
        public static void write16_be(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value >> 8);
            buf[offset + 1] = (byte)value;
        }

        public static void write32_be(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8);
            buf[offset + 3] = (byte)value;
        }

        public static void write32_le(byte[] buf, int offset, uint value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        public static int read32_le(byte[] buf, int offset)
        {
            return buf[offset] |
                   (buf[offset + 1] << 8) |
                   (buf[offset + 2] << 16) |
                   (buf[offset + 3] << 24);
        }

        public static int read16_be(byte[] buf, int offset)
        {
            return (buf[offset] << 8) | buf[offset + 1];
        }

        public static uint spd_crc16(uint crc, byte[] src, int offset, int len)
        {
            crc &= 0xffff;
            for (int i = 0; i < len; i++)
            {
                crc ^= (uint)(src[offset + i] << 8);
                for (int j = 0; j < 8; j++)
                    crc = (crc << 1) ^ ((0u - (crc >> 15)) & 0x11021);
            }
            return crc;
        }

        public static uint spd_checksum(uint crc, byte[] src, int offset, int len, int final)
        {
            int i = offset;
            while (len > 1)
            {
                crc += (uint)(src[i + 1] << 8 | src[i]);
                i += 2;
                len -= 2;
            }
            if (len > 0) crc += src[i];
            if (final != 0)
            {
                crc = (crc >> 16) + (crc & 0xffff);
                crc += crc >> 16;
                crc = ~crc & 0xffff;
                if (len < final)
                    crc = (crc >> 8) | ((crc & 0xff) << 8);
            }
            return crc;
        }

        public static int spd_transcode(byte[] dst, int dstOff, byte[] src, int srcOff, int len)
        {
            int n = 0;
            for (int i = 0; i < len; i++)
            {
                int a = src[srcOff + i];
                if (a == HDLC_HEADER || a == HDLC_ESCAPE)
                {
                    dst[dstOff + n] = HDLC_ESCAPE;
                    n++;
                    a ^= 0x20;
                }
                dst[dstOff + n] = (byte)a;
                n++;
            }
            return n;
        }

        public static int copy_to_wstr(byte[] buf, int offset, int n, string s)
        {
            int a = -1;
            for (int i = 0; a != 0 && i < n; i++)
            {
                a = (i < s.Length) ? s[i] : 0;
                buf[offset + i * 2] = (byte)(a & 0xFF);
                buf[offset + i * 2 + 1] = (byte)((a >> 8) & 0xFF);
            }
            return a;
        }

        public static int copy_from_wstr(char[] d, int n, byte[] buf, int offset)
        {
            int a = -1;
            for (int i = 0; a != 0 && i < n; i++)
            {
                a = buf[offset + i * 2] | (buf[offset + i * 2 + 1] << 8);
                d[i] = (char)a;
                if ((a >> 8) != 0) break;
            }
            return a;
        }
    }
}
