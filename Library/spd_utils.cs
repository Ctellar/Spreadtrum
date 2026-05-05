using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreadtrum.Library
{
    internal class spd_utils
    {
        private const int PROGRESS_BAR_WIDTH = 40;
        private static int completed0 = 0;
        private static ulong done0 = 0;

        [DllImport("kernel32.dll")]
        public static extern ulong GetTickCount64();

        public static void print_progress_bar(ulong done, ulong total, ulong time0)
        {
            ulong time = GetTickCount64();

            if (completed0 == PROGRESS_BAR_WIDTH)
            {
                completed0 = 0;
                done0 = 0;
            }

            int completed = (int)(PROGRESS_BAR_WIDTH * done / (double)total);

            if (completed != completed0)
            {
                int remaining = PROGRESS_BAR_WIDTH - completed;

                Console.Error.Write("[");
                for (int i = 0; i < completed; i++) Console.Error.Write("=");
                for (int i = 0; i < remaining; i++) Console.Error.Write(" ");
                Console.Error.Write("]");

                double percent = 100.0 * done / total;
                double speed = (1000.0 * done / (time - time0)) / 1024.0 / 1024.0;

                Console.Error.Write($"{percent,6:F1}% Speed:{speed,6:F2}Mb/s\r");

                completed0 = completed;
                done0 = done;
            }
        }

        public static string FormatSize(ulong bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        public static void print(string msg)
        {
            Console.Error.WriteLine(msg);
        }

        public static void print_string(byte[] buf, int offset, int n)
        {
            Console.Error.Write("\"");
            for (int i = 0; i < n; i++)
            {
                int a = buf[offset + i];
                int b = 0;
                switch (a)
                {
                    case (int)'"': case (int)'\\': b = a; break;
                    case 0: b = '0'; break;
                    case '\b': b = 'b'; break;
                    case '\t': b = 't'; break;
                    case '\n': b = 'n'; break;
                    case '\f': b = 'f'; break;
                    case '\r': b = 'r'; break;
                }
                if (b != 0)
                    Console.Error.Write("\\" + (char)b);
                else if (a >= 32 && a < 127)
                    Console.Error.Write((char)a);
                else
                    Console.Error.Write($"\\x{a:X2}");
            }
            Console.Error.Write("\"\n");
        }

        public static void print_mem(byte[] buf, int offset, int len)
        {
            for (int i = 0; i < len; i += 16)
            {
                int n = len - i;
                if (n > 16) n = 16;

                for (int j = 0; j < n; j++)
                    Console.Error.Write($"{buf[offset + i + j]:x2} ");
                for (int j = n; j < 16; j++)
                    Console.Error.Write("   ");

                Console.Error.Write(" |");

                for (int j = 0; j < n; j++)
                {
                    int a = buf[offset + i + j];
                    char c = (a > 0x20 && a < 0x7f) ? (char)a : '.';
                    Console.Error.Write(c);
                }

                Console.Error.Write("|\n");
            }
        }

        public static void SendLog(string text, Color? colors = null, bool breakline = false, bool bold = false, bool clear = false)
        {
            if (text == null)
            {
                return;
            }
            if (breakline)
            {
                Console.Error.Write("\n" + text);
            }
            else
            {
                Console.Error.Write(text);
            }
        }
    }
}
