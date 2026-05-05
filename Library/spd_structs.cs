using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spreadtrum.Library
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct efi_header
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] signature;
        public uint revision;
        public uint header_size;
        public uint header_crc32;
        public int reserved;
        public ulong current_lba;
        public ulong backup_lba;
        public ulong first_usable_lba;
        public ulong last_usable_lba;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] disk_guid;
        public ulong partition_entry_lba;
        public int number_of_partition_entries;
        public uint size_of_partition_entry;
        public uint partition_entry_array_crc32;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct efi_entry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] partition_type_guid;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] unique_partition_guid;
        public ulong starting_lba;
        public ulong ending_lba;
        public long attributes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 72)]
        public byte[] partition_name;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DA_INFO_T
    {
        public uint dwVersion;
        public uint bDisableHDLC;
        public byte bIsOldMemory;
        public byte bSupportRawData;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] bReserve;
        public uint dwFlushSize;
        public uint dwStorageType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 59)]
        public uint[] dwReserve;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct slot_metadata
    {
        private byte _data;

        public byte priority
        {
            get => (byte)(_data & 0x0F);
            set => _data = (byte)((_data & 0xF0) | (value & 0x0F));
        }

        public byte tries_remaining
        {
            get => (byte)((_data >> 4) & 0x07);
            set => _data = (byte)((_data & 0x8F) | ((value & 0x07) << 4));
        }

        public byte successful_boot
        {
            get => (byte)((_data >> 7) & 0x01);
            set => _data = (byte)((_data & 0x7F) | ((value & 0x01) << 7));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct bootloader_control
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] slot_suffix;
        public uint magic;
        public byte version;

        private byte _bitfield1;
        public byte nb_slot
        {
            get => (byte)(_bitfield1 & 0x07);
            set => _bitfield1 = (byte)((_bitfield1 & 0xF8) | (value & 0x07));
        }

        private byte _bitfield2;
        public byte recovery_tries_remaining
        {
            get => (byte)(_bitfield2 & 0x07);
            set => _bitfield2 = (byte)((_bitfield2 & 0xF8) | (value & 0x07));
        }

        private byte _bitfield3;
        public byte merge_status
        {
            get => (byte)(_bitfield3 & 0x07);
            set => _bitfield3 = (byte)((_bitfield3 & 0xF8) | (value & 0x07));
        }

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] reserved0;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public slot_metadata[] slot_info;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] reserved1;

        public uint crc32_le;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct pkt_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
        public ushort[] name;
        public uint size;
        public uint size_hi;
        public ulong dummy;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct pkt_nv
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
        public ushort[] name;

        public uint size;
        public uint cs;
    }

    public struct partition_t
    {
        public string name;
        public long size;
    }
}
