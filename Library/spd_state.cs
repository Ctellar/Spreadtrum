using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Spreadtrum.Library.spd_utils;

namespace Spreadtrum.Library
{
    internal class spd_state
    {
        public static int fdl1_loaded = 0;
        public static int fdl2_executed = 0;
        public static int keep_charge = 1;
        public static int highspeed = 0;
        public static int baudrate = 0;
        public static int selected_ab = -1;
        public static int gpt_failed = -1;

        public static ulong fblk_size = 0;
        public static int blk_size = 0;

        public static string fn_partlist = "";
        public static string fn_pgpt = "";
        public static string fn_sprdpart = "";
        public static string savepath = "";

        public static void reset_state()
        {
            fdl1_loaded = 0;
            fdl2_executed = 0;
            keep_charge = 1;
            highspeed = 0;
            baudrate = 0;
            selected_ab = -1;
            gpt_failed = -1;
            fblk_size = 0;
            blk_size = 0;

            fn_partlist = Path.Combine(AppContext.BaseDirectory, "data", "gpt", $"partition_{GetTickCount64()}.xml");
            fn_pgpt = Path.Combine(AppContext.BaseDirectory, "data", "gpt", "pgpt.bin");
            fn_sprdpart = Path.Combine(AppContext.BaseDirectory, "data", "gpt", "sprdpart.bin");
            savepath = Path.Combine(AppContext.BaseDirectory, "data", "temp");
        }
    }
}
