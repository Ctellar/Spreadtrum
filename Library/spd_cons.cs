using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreadtrum.Library
{
    internal class spd_cons
    {
        public const byte HDLC_HEADER = 0x7e;
        public const byte HDLC_ESCAPE = 0x7d;

        public const int FLAGS_CRC16 = 1;
        public const int FLAGS_TRANSCODE = 2;

        public const int CHK_FIXZERO = 1;
        public const int CHK_ORIG = 2;
        public const int RECV_BUF_LEN = 0x8000;

        public const int SECTOR_SIZE = 512;
        public const int MAX_SECTORS = 32;
    }
}
