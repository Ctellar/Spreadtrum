using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Spreadtrum.Library.spd_channel;
using static Spreadtrum.Library.spd_cmd;
using static Spreadtrum.Library.spd_cons;
using static Spreadtrum.Library.spd_crc;
using static Spreadtrum.Library.spd_field;
using static Spreadtrum.Library.spd_state;
using static Spreadtrum.Library.spd_utils;
using static Spreadtrum.Library.spdio_t;

namespace Spreadtrum.Library
{
    internal class spd_main
    {
        public static DA_INFO_T Da_Info;
        public static partition_t gPartInfo;

        public static spdio_t spd_init(Device device)
        {
            spdio_t io = spdio_init(0);
            bool connected = call_ConnectChannel(device.COM!);
            if (!connected)
            {
                throw new Exception($"Failed to connect.");
            }
            reset_state();
            return io;
        }

        public static void spd_handshake(spdio_t io)
        {
            int ret;
            int stage = -1;

            io.flags |= FLAGS_TRANSCODE;

            if (stage != -1)
            {
                io.flags &= ~FLAGS_CRC16;
                encode_msg_nocpy(io, (int)BSL_CMD_CONNECT, 0);
            }
            else
            {
                encode_msg(io, (int)BSL_CMD_CHECK_BAUD, new byte[1], 1);
            }

            for (int i = 0; ; i++)
            {
                if (io.big_buf[io.recv_off + 2] == (int)BSL_REP_VER)
                {
                    ret = (int)BSL_REP_VER;
                    Buffer.BlockCopy(io.big_buf, io.recv_off + 5, io.big_buf, io.raw_off + 4, 5);
                    io.big_buf[io.raw_off + 2] = 0;
                    io.big_buf[io.raw_off + 3] = 5;
                    io.big_buf[io.recv_off + 2] = 0;
                }
                else if (io.big_buf[io.recv_off + 2] == (int)BSL_REP_VERIFY_ERROR || io.big_buf[io.recv_off + 2] == (int)BSL_REP_UNSUPPORTED_COMMAND)
                {
                    if (fdl1_loaded == 0)
                    {
                        ret = io.big_buf[io.recv_off + 2];
                        io.big_buf[io.recv_off + 2] = 0;
                    }
                    else
                    {
                        throw new Exception("wrong command or wrong mode detected.");
                    }
                }
                else
                {
                    send_msg(io);
                    recv_msg(io);
                    ret = recv_type(io);
                }

                if (ret == (int)BSL_REP_ACK || ret == (int)BSL_REP_VER || ret == (int)BSL_REP_VERIFY_ERROR)
                {
                    if (ret == (int)BSL_REP_VER)
                    {
                        if (fdl1_loaded == 1)
                        {
                            print("CHECK_BAUD FDL1");
                            if (Encoding.ASCII.GetString(io.big_buf, io.raw_off + 4, 5) == "SPRD4")
                                fdl2_executed = -1;
                        }
                        else
                        {
                            print("CHECK_BAUD bootrom");
                            if (Encoding.ASCII.GetString(io.big_buf, io.raw_off + 4, 5) == "SPRD4")
                            {
                                fdl1_loaded = -1;
                                fdl2_executed = -1;
                            }
                        }

                        print("BSL_REP_VER: ");
                        int size = read16_be(io.big_buf, io.raw_off + 2);
                        print_string(io.big_buf, io.raw_off + 4, size);

                        string boot_version = Encoding.ASCII.GetString(io.big_buf, io.raw_off + 4, 5);
                        if (!string.IsNullOrEmpty(boot_version))
                        {
                            SendLog("Boot version: ", null, true);
                            SendLog(boot_version, Color.Blue);
                        }

                        encode_msg_nocpy(io, (int)BSL_CMD_CONNECT, 0);
                        if (send_and_check(io) != 0)
                            throw new Exception("spd handshake failed.");
                    }
                    else if (ret == (int)BSL_REP_VERIFY_ERROR)
                    {
                        encode_msg_nocpy(io, (int)BSL_CMD_CONNECT, 0);
                        if (fdl1_loaded != 1)
                        {
                            if (send_and_check(io) != 0)
                                throw new Exception("spd handshake failed.");
                        }
                        else
                        {
                            i = -1;
                            continue;
                        }
                    }

                    if (fdl1_loaded == 1)
                    {
                        print("CMD_CONNECT FDL1");
                        if (keep_charge != 0)
                        {
                            encode_msg_nocpy(io, (int)BSL_CMD_KEEP_CHARGE, 0);
                            if (send_and_check(io) == 0)
                                print("KEEP_CHARGE FDL1");
                        }
                    }
                    else
                    {
                        print("CMD_CONNECT bootrom");
                    }
                    break;
                }
                else if (ret == (int)BSL_REP_UNSUPPORTED_COMMAND)
                {
                    encode_msg_nocpy(io, (int)BSL_CMD_DISABLE_TRANSCODE, 0);
                    if (send_and_check(io) == 0)
                    {
                        io.flags &= ~FLAGS_TRANSCODE;
                        print("DISABLE_TRANSCODE");
                    }
                    fdl2_executed = 1;
                    break;
                }
                else if (i == 4)
                {
                    if (stage != -1)
                    {
                        throw new Exception("wrong command or wrong mode detected.");
                    }
                    else
                    {
                        encode_msg_nocpy(io, (int)BSL_CMD_CONNECT, 0);
                        stage++;
                        i = -1;
                    }
                }
            }
        }

        public static void spd_rehandshake(spdio_t io, uint addr)
        {
            print("EXEC FDL1");

            if (addr == 0x5500 || addr == 0x65000800)
            {
                highspeed = 1;
                if (baudrate == 0) baudrate = 921600;
            }

            io.flags &= ~FLAGS_CRC16;

            encode_msg(io, (int)BSL_CMD_CHECK_BAUD, new byte[1], 1);
            for (int i = 0; ; i++)
            {
                send_msg(io);
                recv_msg(io);
                if (recv_type(io) == (int)BSL_REP_VER) break;

                print("CHECK_BAUD FAIL");
                if (i == 4)
                    throw new Exception("wrong command or wrong mode detected.");

                Thread.Sleep(500);
            }
            print("CHECK_BAUD FDL1");

            print("BSL_REP_VER: ");
            int strLen = read16_be(io.big_buf, io.raw_off + 2);
            print_string(io.big_buf, io.raw_off + 4, strLen);

            string boot_name = Encoding.ASCII.GetString(io.big_buf, io.raw_off + 4, strLen);
            if (!string.IsNullOrEmpty(boot_name))
            {
                SendLog("Boot information: ", null, true);
                SendLog(boot_name, Color.Blue);
            }

            if (Encoding.ASCII.GetString(io.big_buf, io.raw_off + 4, 5) == "SPRD4")
                fdl2_executed = -1;

            encode_msg_nocpy(io, (int)BSL_CMD_CONNECT, 0);
            if (send_and_check(io) != 0)
                throw new Exception("CMD_CONNECT failed");
            print("CMD_CONNECT FDL1");

            if (baudrate != 0)
            {
                int p0 = io.temp_off;
                write32_be(io.big_buf, p0, (uint)baudrate);

                encode_msg_nocpy(io, (int)BSL_CMD_CHANGE_BAUD, 4);
                if (send_and_check(io) == 0)
                {
                    print($"CHANGE_BAUD FDL1 to {baudrate}");
                    call_SetProperty(baudrate);
                }
            }

            if (keep_charge != 0)
            {
                encode_msg_nocpy(io, (int)BSL_CMD_KEEP_CHARGE, 0);
                if (send_and_check(io) == 0)
                    print("KEEP_CHARGE FDL1");
            }

            fdl1_loaded = 1;
        }

        public static byte[]? loadfile(string fn, out long num, long extra)
        {
            num = 0;
            byte[]? buf = null;

            if (File.Exists(fn))
            {
                using (FileStream fs = new FileStream(fn, FileMode.Open, FileAccess.Read))
                {
                    long n = fs.Length;
                    if (n > 0)
                    {
                        buf = new byte[n + extra];
                        int j = fs.Read(buf, 0, (int)n);
                        num = j;
                    }
                }
            }
            return buf;
        }

        public static void send_buf(spdio_t io, uint start_addr, int end_data, uint step, byte[] mem, uint size, int src_offs)
        {
            uint i, n;
            ulong time_start = GetTickCount64();
            int p0 = io.temp_off;
            write32_be(io.big_buf, p0, start_addr);
            write32_be(io.big_buf, p0 + 4, size);

            encode_msg_nocpy(io, (int)BSL_CMD_START_DATA, 8);
            if (send_and_check(io) != 0) return;

            for (i = 0; i < size; i += n)
            {
                n = size - i;
                if (n > step) n = step;

                byte[] chunk = new byte[n];
                Buffer.BlockCopy(mem, src_offs + (int)i, chunk, 0, (int)n);

                encode_msg(io, (int)BSL_CMD_MIDST_DATA, chunk, (int)n);
                if (send_and_check(io) != 0) return;

                print_progress_bar((ulong)i + (ulong)n, (ulong)size, time_start);
            }

            if (end_data != 0)
            {
                encode_msg_nocpy(io, (int)BSL_CMD_END_DATA, 0);
                send_and_check(io);
            }
        }

        public static long send_file(spdio_t io, string fn, uint start_addr, int end_data, uint step, uint src_offs, uint src_size)
        {
            long size = 0;
            byte[]? mem = loadfile(fn, out size, 0);
            if (mem == null)
                throw new Exception($"loadfile(\"{fn}\") failed");

            if (((ulong)size >> 32) != 0)
                throw new Exception("file too big");

            if (size < src_offs)
                throw new Exception("required offset larger than file size");

            size -= src_offs;
            if (src_size != 0)
            {
                if (size < src_size)
                    print("required size larger than file size");
                else
                    size = src_size;
            }

            send_buf(io, start_addr, end_data, step, mem, (uint)size, (int)src_offs);

            print($"SEND {fn} to 0x{start_addr:X}");
            return size;
        }

        public static void encode_msg(spdio_t io, int type, byte[] data, int len)
        {
            if (len > 0xffff)
                throw new Exception("message too long");

            io.send_buf = io.enc_buf;

            if (type == (int)BSL_CMD_CHECK_BAUD)
            {
                for (int i = 0; i < len; i++)
                    io.big_buf[io.enc_off + i] = HDLC_HEADER;

                io.enc_len = len;
                io.big_buf[io.untrans_off + 1] = HDLC_HEADER;
                return;
            }

            int p0 = io.untrans_off + 1;
            int p = p0;

            write16_be(io.big_buf, p, type); p += 2;
            write16_be(io.big_buf, p, len); p += 2;
            Buffer.BlockCopy(data, 0, io.big_buf, p, len);
            p += len;

            int msg_len = p - p0;
            uint chk = ((io.flags & FLAGS_CRC16) != 0)
                ? spd_crc16(0, io.big_buf, p0, msg_len)
                : spd_checksum(0, io.big_buf, p0, msg_len, CHK_FIXZERO);

            write16_be(io.big_buf, p, (int)chk); p += 2;

            io.raw_len = msg_len = p - p0;

            if ((io.flags & FLAGS_TRANSCODE) != 0)
            {
                int enc_p = io.enc_off;
                io.big_buf[enc_p++] = HDLC_HEADER;
                int enc_len = spd_transcode(io.big_buf, enc_p, io.big_buf, p0, msg_len);
                io.big_buf[enc_p + enc_len] = HDLC_HEADER;
                io.enc_len = enc_len + 2;
            }
            else
            {
                int enc_p = io.untrans_off;
                io.big_buf[enc_p++] = HDLC_HEADER;
                io.send_buf = io.untranscode_buf;
                io.big_buf[p0 + msg_len] = HDLC_HEADER;
                io.enc_len = msg_len + 2;
            }
        }

        public static void encode_msg_nocpy(spdio_t io, int type, int len)
        {
            if (len > 0xFFFF)
                throw new Exception("message too long");

            io.send_buf = io.enc_buf;

            if (type == (int)BSL_CMD_CHECK_BAUD)
            {
                for (int i = 0; i < len; i++)
                    io.big_buf[io.enc_off + i] = HDLC_HEADER;

                io.enc_len = len;
                io.big_buf[io.untrans_off + 1] = HDLC_HEADER;
                return;
            }

            int p0 = io.untrans_off + 1;
            int p = p0;

            write16_be(io.big_buf, p, type); p += 2;
            write16_be(io.big_buf, p, len); p += 2;

            Buffer.BlockCopy(io.big_buf, io.temp_off, io.big_buf, p, len);
            p += len;

            int msg_len = p - p0;
            uint chk = ((io.flags & FLAGS_CRC16) != 0)
                ? spd_crc16(0, io.big_buf, p0, msg_len)
                : spd_checksum(0, io.big_buf, p0, msg_len, CHK_FIXZERO);

            write16_be(io.big_buf, p, (int)chk); p += 2;

            io.raw_len = msg_len = p - p0;

            if ((io.flags & FLAGS_TRANSCODE) != 0)
            {
                int enc_p = io.enc_off;
                io.big_buf[enc_p++] = HDLC_HEADER;
                int enc_len = spd_transcode(io.big_buf, enc_p, io.big_buf, p0, msg_len);
                io.big_buf[enc_p + enc_len] = HDLC_HEADER;
                io.enc_len = enc_len + 2;
            }
            else
            {
                int enc_p = io.untrans_off;
                io.big_buf[enc_p++] = HDLC_HEADER;
                io.send_buf = io.untranscode_buf;
                io.big_buf[p0 + msg_len] = HDLC_HEADER;
                io.enc_len = msg_len + 2;
            }
        }

        public static int send_msg(spdio_t io)
        {
            if (io.enc_len == 0)
                throw new Exception("empty message");

            if (io.verbose >= 2)
            {
                print($"send ({io.enc_len}):");
                print_mem(io.big_buf, io.send_buf.Offset, io.enc_len);
            }
            else if (io.verbose >= 1)
            {
                if (io.big_buf[io.untrans_off + 1] == HDLC_HEADER)
                    print("send: check baud");
                else if (io.raw_len >= 4)
                {
                    int type = read16_be(io.big_buf, io.untrans_off + 1);
                    int size = read16_be(io.big_buf, io.untrans_off + 3);
                    print($"send: type = 0x{type:X2}, size = {size}");
                }
                else
                    print("send: unknown message");
            }

            byte[] sendData = new byte[io.enc_len];
            Buffer.BlockCopy(io.big_buf, io.send_buf.Offset, sendData, 0, io.enc_len);

            int ret = call_Write(sendData, 0, io.enc_len);
            if (ret != io.enc_len)
                throw new Exception($"usb_send failed ({ret} / {io.enc_len})");

            return ret;
        }

        public static int recv_msg(spdio_t io)
        {
            int ret;
            while (true)
            {
                if (io.m_dwRecvThreadID != 0)
                    ret = recv_msg_async(io);
                else
                    ret = recv_msg_orig(io);

                if (ret == 0)
                {
                    if (fdl2_executed != 0)
                    {
                        if (io.raw_len > 0)
                        {
                            call_Clear();
                            io.raw_len = 0;
                        }
                        send_msg(io);

                        if (io.m_dwRecvThreadID != 0)
                            ret = recv_msg_async(io);
                        else
                            ret = recv_msg_orig(io);

                        if (ret == 0) break;
                    }
                    else break;
                }

                if (recv_type(io) != (int)BSL_REP_LOG) break;

                print("BSL_REP_LOG: ");
                int size = read16_be(io.big_buf, io.raw_off + 2);
                print_string(io.big_buf, io.raw_off + 4, size);
            }
            return ret;
        }

        public static int recv_type(spdio_t io)
        {
            return read16_be(io.big_buf, io.raw_off);
        }

        public static int send_and_check(spdio_t io)
        {
            int ret;

            send_msg(io);

            ret = recv_msg(io);
            if (ret == 0)
                throw new Exception("timeout reached");

            ret = recv_type(io);
            if (ret != (int)BSL_REP_ACK)
            {
                print($"unexpected response (0x{ret:X4})");
                return -1;
            }

            return 0;
        }

        public static int recv_msg_async(spdio_t io)
        {
            bool signaled = io.m_hOprEvent!.WaitOne(io.timeout);
            if (!signaled)
            {
                return 0;
            }
            else
            {
                io.m_hOprEvent.Reset();
                return io.raw_len;
            }
        }

        public static int recv_msg_orig(spdio_t io)
        {
            int plen = 6;
            Array.Clear(io.big_buf, io.recv_off, 8);

            while (true)
            {
                if (recv_read_data(io) == 0) return 0;
                if (recv_transcode(io, io.big_buf, io.recv_off, io.recv_len, ref plen) == 0) return 0;
                if (plen == io.raw_len) break;
            }
            return recv_check_crc(io);
        }

        public static int recv_read_data(spdio_t io)
        {
            int len = call_Read(io.big_buf, io.recv_off, RECV_BUF_LEN, io.timeout);
            if (len < 0)
            {
                print($"usb_recv failed, ret = {len}");
                return 0;
            }

            if (io.verbose >= 2)
            {
                print($"recv ({len}):");
                print_mem(io.big_buf, io.recv_off, len);
            }

            io.recv_len = len;
            return len;
        }

        public static int recv_transcode(spdio_t io, byte[] buf, int buf_off, int buf_len, ref int plen)
        {
            int a, pos = 0, nread = io.raw_len;
            bool head_found = false;
            int esc = 0;
            if (plen == 6) nread = 0;
            if (nread > 0) head_found = true;

            while (pos < buf_len)
            {
                a = buf[buf_off + pos++];
                if ((io.flags & FLAGS_TRANSCODE) != 0)
                {
                    if (esc != 0 && a != (HDLC_HEADER ^ 0x20) && a != (HDLC_ESCAPE ^ 0x20))
                    {
                        print($"unexpected escaped byte (0x{a:X2})");
                        return 0;
                    }
                    if (a == HDLC_HEADER)
                    {
                        if (!head_found) head_found = true;
                        else if (nread == 0) continue;
                        else if (nread < plen)
                        {
                            print("received message too short");
                            return 0;
                        }
                        else break;
                    }
                    else if (a == HDLC_ESCAPE)
                    {
                        esc = 0x20;
                    }
                    else
                    {
                        if (!head_found) continue;
                        if (nread >= plen)
                        {
                            print("received message too long");
                            return 0;
                        }
                        io.big_buf[io.raw_off + nread++] = (byte)(a ^ esc);
                        esc = 0;
                    }
                }
                else
                {
                    if (!head_found && a == HDLC_HEADER)
                    {
                        head_found = true;
                        continue;
                    }
                    if (nread == plen)
                    {
                        if (a != HDLC_HEADER)
                        {
                            print("expected end of message");
                            return 0;
                        }
                        break;
                    }
                    io.big_buf[io.raw_off + nread++] = (byte)a;
                }

                if (nread == 4)
                {
                    a = read16_be(io.big_buf, io.raw_off + 2);
                    plen = a + 6;
                }
            }

            io.raw_len = nread;
            return nread;
        }

        public static int recv_check_crc(spdio_t io)
        {
            int nread = io.raw_len;
            int plen = read16_be(io.big_buf, io.raw_off + 2) + 6;

            if (nread < 6)
            {
                print("received message too short");
                return 0;
            }

            if (nread != plen)
            {
                print($"bad length ({nread}, expected {plen})");
                return 0;
            }

            int a = read16_be(io.big_buf, io.raw_off + plen - 2);

            if (fdl1_loaded == 0 && (io.flags & FLAGS_CRC16) == 0)
            {
                int chk1 = (int)spd_crc16(0, io.big_buf, io.raw_off, plen - 2);
                if (a == chk1)
                {
                    io.flags |= FLAGS_CRC16;
                }
                else
                {
                    int chk2 = (int)spd_checksum(0, io.big_buf, io.raw_off, plen - 2, CHK_ORIG);
                    if (a == chk2)
                    {
                        fdl1_loaded = 1;
                    }
                    else
                    {
                        print($"bad checksum (0x{a:X4}, expected 0x{chk1:X4} or 0x{chk2:X4})");
                        return 0;
                    }
                }
            }
            else
            {
                int chk = (io.flags & FLAGS_CRC16) != 0
                    ? (int)spd_crc16(0, io.big_buf, io.raw_off, plen - 2)
                    : (int)spd_checksum(0, io.big_buf, io.raw_off, plen - 2, CHK_ORIG);

                if (a != chk)
                {
                    print($"bad checksum (0x{a:X4}, expected 0x{chk:X4})");
                    return 0;
                }
            }

            if (io.verbose == 1)
            {
                int type = read16_be(io.big_buf, io.raw_off);
                int size = read16_be(io.big_buf, io.raw_off + 2);
                print($"recv: type = 0x{type:X2}, size = {size}");
            }

            return nread;
        }

        public static int recv_msg_timeout(spdio_t io, int timeout)
        {
            int old = io.timeout;
            io.timeout = old > timeout ? old : timeout;
            int ret = recv_msg(io);
            io.timeout = old;
            return ret;
        }

        public static void read_gpt_da_info(spdio_t io)
        {
            int ret;

            Da_Info = new DA_INFO_T();

            encode_msg_nocpy(io, (int)BSL_CMD_EXEC_DATA, 0);
            send_msg(io);

            ret = recv_msg_timeout(io, 15000);
            if (ret == 0) throw new Exception("timeout reached");

            ret = recv_type(io);
            if (ret == (int)BSL_REP_INCOMPATIBLE_PARTITION)
            {
                get_Da_Info(io);
            }
            else if (ret != (int)BSL_REP_ACK)
            {
                throw new Exception($"unexpected response (0x{ret:X4})");
            }

            print("EXEC FDL2");

            if (Da_Info.bDisableHDLC != 0)
            {
                encode_msg_nocpy(io, (int)BSL_CMD_DISABLE_TRANSCODE, 0);
                if (send_and_check(io) == 0)
                {
                    io.flags &= ~FLAGS_TRANSCODE;
                    print("DISABLE_TRANSCODE");
                }
            }

            if (Da_Info.bSupportRawData != 0)
            {
                blk_size = 0xf800;
                io.ptable = partition_list(io, fn_partlist, out io.part_count)!;

                if (fdl2_executed != 0)
                {
                    Da_Info.bSupportRawData = 0;
                    print("DISABLE_WRITE_RAW_DATA in SPRD4");
                }
                else
                {
                    encode_msg_nocpy(io, (int)BSL_CMD_ENABLE_RAW_DATA, 0);
                    if (send_and_check(io) == 0)
                        print("ENABLE_WRITE_RAW_DATA");
                }
            }
            else if (highspeed != 0 || Da_Info.dwStorageType == 0x103)
            {
                blk_size = 0xf800;
                io.ptable = partition_list(io, fn_partlist, out io.part_count)!;
            }
            else if (Da_Info.dwStorageType == 0x102)
            {
                io.ptable = partition_list(io, fn_partlist, out io.part_count)!;
            }
            else if (Da_Info.dwStorageType == 0x101)
            {
                print("Storage is nand");
            }

            if (gpt_failed != 1)
            {
                if (selected_ab == 2)
                    print("Device is using slot b");
                else if (selected_ab == 1)
                    print("Device is using slot a");
                else
                {
                    print("Device is not using VAB");
                    if (Da_Info.bSupportRawData != 0)
                    {
                        print($"RAW_DATA level is {Da_Info.bSupportRawData}, but DISABLED for stability, you can set it manually");
                        Da_Info.bSupportRawData = 0;
                    }
                }

                SendLog("Device A/B: ", null, true);
                if (selected_ab == 1)
                    SendLog("A", Color.Blue);
                else if (selected_ab == 2)
                    SendLog("B", Color.Blue);
                else
                    SendLog("Device is not using VAB", Color.Blue);

                SendLog("Storage type: ", null, true);
                if (Da_Info.dwStorageType == 0x102)
                    SendLog("EMMC", Color.Blue);
                else if (Da_Info.dwStorageType == 0x103)
                    SendLog("UFS", Color.Blue);
                else if (Da_Info.dwStorageType == 0x101)
                    SendLog("NAND", Color.Blue);

                SendLog("Total partitions: ", null, true);
                SendLog($"{io.part_count}\n", Color.Blue);
            }

            fdl2_executed = 1;
        }

        public static ulong dump_partition(spdio_t io, string name, ulong start, ulong len, string fn, uint step, bool show_prog = true)
        {
            uint n, nread, t32;
            ulong offset, n64, saved_size = 0;
            int ret;
            int mode64 = (int)((start + len) >> 32);
            string nameTmp = name;

            if (name == "super")
            {
                dump_partition(io, "metadata", 0, (ulong)check_partition(io, "metadata", 1), Path.Combine(Path.GetDirectoryName(fn)!, "metadata.bin"), step);
            }
            else if (name.StartsWith("userdata"))
            {
                throw new Exception("check_confirm not implemented");
            }
            else if (name.Contains("nv1"))
            {
                nameTmp = name.Replace("nv1", "nv2");
                name = nameTmp;
                start = 512;
                if (len > 512) len -= 512;
            }

            select_partition(io, name, start + len, mode64, (int)BSL_CMD_READ_START);
            if (send_and_check(io) != 0)
            {
                encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
                send_and_check(io);
                return 0;
            }

            using (var fo = new FileStream(fn, FileMode.Create, FileAccess.Write))
            {
                ulong timeStart = GetTickCount64();
                for (offset = start; (n64 = start + len - offset) > 0;)
                {
                    int p0 = io.temp_off;
                    n = (uint)(n64 > step ? step : n64);

                    write32_le(io.big_buf, p0, (uint)n);
                    write32_le(io.big_buf, p0 + 4, (uint)offset);
                    t32 = (uint)(offset >> 32);
                    write32_le(io.big_buf, p0 + 8, (uint)t32);

                    encode_msg_nocpy(io, (int)BSL_CMD_READ_MIDST, mode64 != 0 ? 12 : 8);
                    send_msg(io);
                    ret = recv_msg(io);
                    if (ret == 0) throw new Exception("timeout reached");

                    if ((ret = recv_type(io)) != (int)BSL_REP_READ_FLASH)
                    {
                        print($"unexpected response (0x{ret:X4})");
                        break;
                    }

                    nread = (uint)read16_be(io.big_buf, io.raw_off + 2);
                    if (n < nread) throw new Exception("unexpected length");

                    fo.Write(io.big_buf, io.raw_off + 4, (int)nread);
                    if (show_prog)
                        print_progress_bar(offset + nread - start, len, timeStart);

                    offset += nread;
                    if (n != nread) break;

                    if (fblk_size != 0)
                    {
                        saved_size += nread;
                        if (saved_size >= fblk_size)
                        {
                            Thread.Sleep(1000);
                            saved_size = 0;
                        }
                    }
                }

                print($"\nRead Part Done: {name}+0x{start:X}, target: 0x{len:X}, read: 0x{(offset - start):X}");
            }

            encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
            send_and_check(io);
            return offset - start;
        }

        public static ulong check_partition(spdio_t io, string name, int need_size)
        {
            uint t32;
            ulong n64;
            ulong offset = 0;
            int ret, i, end = 20;
            string nameTmp = name;

            if (selected_ab > 0 && name == "uboot") return 0;

            if (name.Contains("fixnv"))
            {
                if (selected_ab > 0)
                {
                    if (!name.EndsWith("_a") && !name.EndsWith("_b")) return 0;
                }
                nameTmp = name.Replace("1", "2");
                name = nameTmp;
            }
            else if (name.Contains("runtimenv"))
            {
                if (name.EndsWith("_a") || name.EndsWith("_b")) return 0;
                nameTmp = name.Replace("1", "2");
                name = nameTmp;
            }

            if (selected_ab > 0)
            {
                find_partition_size_new(io, name, ref offset);
                if (offset > 0)
                {
                    return (need_size != 0) ? offset : 1;
                }
            }

            select_partition(io, name, 0x8, 0, (int)BSL_CMD_READ_START);
            if (send_and_check(io) != 0)
            {
                encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
                send_and_check(io);
                return 0;
            }

            int p0 = io.temp_off;
            write32_le(io.big_buf, p0, 8);
            write32_le(io.big_buf, p0 + 4, 0);

            encode_msg_nocpy(io, (int)BSL_CMD_READ_MIDST, 8);
            send_msg(io);
            ret = recv_msg(io);
            if (ret == 0) throw new Exception("timeout reached");
            ret = (recv_type(io) == (int)BSL_REP_READ_FLASH) ? 1 : 0;

            encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
            send_and_check(io);

            if (ret == 0 || need_size == 0) return (ulong)ret;

            int incrementing = 1;
            select_partition(io, name, 0xffffffff, 0, (int)BSL_CMD_READ_START);
            if (send_and_check(io) != 0)
            {
                end = 10;
                encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
                send_and_check(io);

                for (i = 21; i >= end;)
                {
                    n64 = offset + ((1UL << i) - (1UL << end));
                    select_partition(io, name, n64, 0, (int)BSL_CMD_READ_START);
                    send_msg(io);
                    ret = recv_msg(io);
                    if (ret == 0) throw new Exception("timeout reached");
                    ret = recv_type(io);

                    if (incrementing != 0)
                    {
                        if (ret != (int)BSL_REP_ACK)
                        {
                            offset += 1UL << (i - 1);
                            i -= 2;
                            incrementing = 0;
                        }
                        else i++;
                    }
                    else
                    {
                        if (ret == (int)BSL_REP_ACK) offset += 1UL << i;
                        i--;
                    }

                    encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
                    send_and_check(io);
                }
                offset -= 1UL << end;
            }
            else
            {
                for (i = 21; i >= end;)
                {
                    int p1 = io.temp_off;
                    n64 = offset + ((1UL << i) - (1UL << end));
                    write32_le(io.big_buf, p1, 4);
                    write32_le(io.big_buf, p1 + 4, (uint)n64);
                    t32 = (uint)(n64 >> 32);
                    write32_le(io.big_buf, p1 + 8, (uint)t32);

                    encode_msg_nocpy(io, (int)BSL_CMD_READ_MIDST, 12);
                    send_msg(io);
                    ret = recv_msg(io);
                    if (ret == 0) throw new Exception("timeout reached");
                    ret = recv_type(io);

                    if (incrementing != 0)
                    {
                        if (ret != (int)BSL_REP_READ_FLASH)
                        {
                            offset += 1UL << (i - 1);
                            i -= 2;
                            incrementing = 0;
                        }
                        else i++;
                    }
                    else
                    {
                        if (ret == (int)BSL_REP_READ_FLASH) offset += 1UL << i;
                        i--;
                    }
                }
            }

            if (end == 10) Da_Info.dwStorageType = 0x101;
            print($"partition_size_pc: {name}, 0x{offset:X}");
            encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
            send_and_check(io);
            return offset;
        }

        public static void find_partition_size_new(spdio_t io, string name, ref ulong offset)
        {
            int ret;

            string nameTmp = name + "_size";

            select_partition(io, nameTmp, 0x80, 0, (int)BSL_CMD_READ_START);

            if (send_and_check(io) != 0)
            {
                encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
                send_and_check(io);
                return;
            }

            int p0 = io.temp_off;
            write32_le(io.big_buf, p0, 0x80);
            write32_le(io.big_buf, p0 + 4, 0);

            encode_msg_nocpy(io, (int)BSL_CMD_READ_MIDST, 8);
            send_msg(io);
            ret = recv_msg(io);
            if (ret == 0) throw new Exception("timeout reached");

            if (recv_type(io) == (int)BSL_REP_READ_FLASH)
            {
                string resp = Encoding.ASCII.GetString(io.big_buf, io.raw_off + 4, io.raw_len - 4);

                var match1 = System.Text.RegularExpressions.Regex.Match(resp, @"size:[^:]+: 0x([0-9A-Fa-f]+)");
                if (match1.Success)
                {
                    offset = Convert.ToUInt64(match1.Groups[1].Value, 16);
                    ret = 1;
                }
                else
                {
                    var match2 = System.Text.RegularExpressions.Regex.Match(resp, @"partition\s+\S+\s+total size:\s+0x([0-9A-Fa-f]+)");
                    if (match2.Success)
                    {
                        offset = Convert.ToUInt64(match2.Groups[1].Value, 16);
                        ret = 1;
                    }
                }

                print($"partition_size_device: {name}, 0x{offset:X}");
            }

            encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
            send_and_check(io);
        }

        public static partition_t[]? partition_list(spdio_t io, string fn, out int part_count)
        {
            ulong size;
            uint i, n = 0;
            int ret;
            part_count = 0;
            partition_t[] ptable = new partition_t[128];

            print("Reading Partition List");

            if (selected_ab < 0) select_ab(io);

            int verbose = io.verbose;
            io.verbose = 0;
            size = dump_partition(io, "user_partition", 0, 32 * 1024, fn_pgpt, 4096);
            io.verbose = verbose;

            if (32 * 1024 == size)
                gpt_failed = gpt_info(ptable, fn, out part_count);

            if (gpt_failed != 0)
            {
                if (File.Exists(fn_pgpt))
                    File.Delete(fn_pgpt);

                encode_msg_nocpy(io, (int)BSL_CMD_READ_PARTITION, 0);
                send_msg(io);
                ret = recv_msg(io);
                if (ret == 0) throw new Exception("timeout reached");
                ret = recv_type(io);
                if (ret != (int)BSL_REP_READ_PARTITION)
                {
                    print($"unexpected response (0x{ret:X4})");
                    gpt_failed = -1;
                    part_count = 0;
                    return null;
                }

                size = (ulong)read16_be(io.big_buf, io.raw_off + 2);
                if (size % 0x4c != 0)
                {
                    print($"not divisible by struct size (0x{size:X})");
                    gpt_failed = -1;
                    part_count = 0;
                    return null;
                }

                File.WriteAllBytes(fn_sprdpart, io.big_buf.Skip(io.raw_off + 4).Take((int)size).ToArray());
                n = (uint)(size / 0x4c);

                StreamWriter? fo = null;
                if (fn != "-")
                {
                    fo = new StreamWriter(fn);
                    fo.WriteLine("<Partitions>");
                }

                int divisor = 10;
                print("detecting sector size");
                int p = io.raw_off + 4;
                for (i = 0; i < n; i++, p += 0x4c)
                {
                    long sz = read32_le(io.big_buf, p + 0x48);
                    while ((sz >> divisor) == 0) divisor--;
                }
                Da_Info.dwStorageType = (uint)(divisor == 10 ? 0x102 : 0x103);

                p = io.raw_off + 4;
                print($"  0 {"splloader",36}     256KB");
                for (i = 0; i < n; i++, p += 0x4c)
                {
                    char[] namebuf = new char[36];
                    ret = copy_from_wstr(namebuf, 36, io.big_buf, p);
                    if (ret != 0) throw new Exception("bad partition name");

                    string pname = new string(namebuf).TrimEnd('\0');
                    long sz = read32_le(io.big_buf, p + 0x48);
                    long partSize = (long)sz << (20 - divisor);

                    ptable[i].name = pname;
                    ptable[i].size = partSize;

                    print($"{i + 1,3} {pname,36} {partSize >> 20,7}MB");

                    if (fo != null)
                    {
                        fo.Write($"    <Partition id=\"{pname}\" size=\"");
                        if (i + 1 == n) fo.WriteLine($"0x{~0}\"/>");
                        else fo.WriteLine($"{partSize >> 20}\"/>");
                    }

                    if (selected_ab == 0)
                    {
                        if (pname.EndsWith("_a")) selected_ab = 1;
                    }
                }

                if (fo != null)
                {
                    fo.WriteLine("</Partitions>");
                    fo.Close();
                }

                part_count = (int)n;
                print("unable to get standard gpt table");
                print("sprd partition list packet saved to sprdpart.bin");
                gpt_failed = 0;
            }

            if (part_count > 0)
            {
                if (fn != "-") print($"partition list saved to {fn}");
                print($"Total number of partitions: {part_count}");
                if (Da_Info.dwStorageType == 0x102) print("Storage is emmc");
                else if (Da_Info.dwStorageType == 0x103) print("Storage is ufs");
                return ptable;
            }
            else
            {
                gpt_failed = -1;
                return null;
            }
        }

        public static int gpt_info(partition_t[] ptable, string fnXml, out int partCount)
        {
            partCount = 0;
            using (var fs = File.OpenRead(fn_pgpt))
            using (var br = new BinaryReader(fs))
            {
                byte[] buffer = new byte[SECTOR_SIZE];
                int sectorIndex = 0;
                bool found = false;
                efi_header header = default;

                while (sectorIndex < MAX_SECTORS)
                {
                    int bytesRead = br.Read(buffer, 0, SECTOR_SIZE);
                    if (bytesRead != SECTOR_SIZE) return -1;

                    if (Encoding.ASCII.GetString(buffer, 0, 8) == "EFI PART")
                    {
                        header = parse_efi_header(buffer, 0);
                        found = true;
                        break;
                    }
                    sectorIndex++;
                }

                if (!found) return -1;
                Da_Info.dwStorageType = (uint)(sectorIndex == 1 ? 0x102 : 0x103);

                int realSectorSize = SECTOR_SIZE * sectorIndex;
                fs.Seek((long)header.partition_entry_lba * realSectorSize, SeekOrigin.Begin);

                int entryCount = header.number_of_partition_entries;
                efi_entry[] entries = new efi_entry[entryCount];
                for (int i = 0; i < entryCount; i++)
                {
                    entries[i] = parse_efi_entry(br);
                }

                StreamWriter? fo = null;
                if (fnXml != "-")
                {
                    fo = new StreamWriter(fnXml);
                    fo.WriteLine("<Partitions>");
                }

                int n = 0;
                for (int i = 0; i < entryCount; i++)
                {
                    if (entries[i].starting_lba == 0 && entries[i].ending_lba == 0)
                    {
                        n = i;
                        break;
                    }
                }

                print($"  0 {"splloader",36}     256KB");
                for (int i = 0; i < n; i++)
                {
                    char[] namebuf = new char[36];
                    copy_from_wstr(namebuf, 36, entries[i].partition_name, 0);
                    string pname = new string(namebuf).TrimEnd('\0');

                    ulong lbaCount = entries[i].ending_lba - entries[i].starting_lba + 1;
                    long partSize = (long)lbaCount * realSectorSize;

                    ptable[i].name = pname;
                    ptable[i].size = partSize;

                    print($"{i + 1,3} {pname,36} {partSize >> 20,7}MB");

                    if (fnXml != "-")
                    {
                        fo!.Write($"    <Partition id=\"{pname}\" size=\"");
                        if (i + 1 == n) fo.WriteLine("0xFFFFFFFF\"/>");
                        else fo.WriteLine($"{partSize >> 20}\"/>");
                    }

                    if (selected_ab == 0 && pname.EndsWith("_a"))
                        selected_ab = 1;
                }

                if (fo != null)
                {
                    fo.WriteLine("</Partitions>");
                    fo.Close();
                }

                partCount = n;
                print("standard gpt table saved to pgpt.bin");
                print("skip saving sprd partition list packet");
                return 0;
            }
        }

        public static efi_header parse_efi_header(byte[] buffer, int offset)
        {
            if (buffer.Length < offset + 92)
                throw new ArgumentException("Buffer too small.");

            var header = new efi_header
            {
                signature = new byte[8],
                disk_guid = new byte[16]
            };

            Buffer.BlockCopy(buffer, offset, header.signature, 0, 8);

            header.revision = BitConverter.ToUInt32(buffer, offset + 8);
            header.header_size = BitConverter.ToUInt32(buffer, offset + 12);
            header.header_crc32 = BitConverter.ToUInt32(buffer, offset + 16);
            header.reserved = BitConverter.ToInt32(buffer, offset + 20);

            header.current_lba = BitConverter.ToUInt64(buffer, offset + 24);
            header.backup_lba = BitConverter.ToUInt64(buffer, offset + 32);
            header.first_usable_lba = BitConverter.ToUInt64(buffer, offset + 40);
            header.last_usable_lba = BitConverter.ToUInt64(buffer, offset + 48);

            Buffer.BlockCopy(buffer, offset + 56, header.disk_guid, 0, 16);

            header.partition_entry_lba = BitConverter.ToUInt64(buffer, offset + 72);
            header.number_of_partition_entries = BitConverter.ToInt32(buffer, offset + 80);
            header.size_of_partition_entry = BitConverter.ToUInt32(buffer, offset + 84);
            header.partition_entry_array_crc32 = BitConverter.ToUInt32(buffer, offset + 88);

            return header;
        }

        public static efi_entry parse_efi_entry(BinaryReader br) 
        {
            byte[] buff = br.ReadBytes(128);

            if (buff.Length != 128)
                throw new Exception("Invalid GPT entry size.");

            var entry = new efi_entry
            {
                partition_type_guid = new byte[16],
                unique_partition_guid = new byte[16],
                partition_name = new byte[72]
            };

            Buffer.BlockCopy(buff, 0, entry.partition_type_guid, 0, 16);
            Buffer.BlockCopy(buff, 16, entry.unique_partition_guid, 0, 16);

            entry.starting_lba = BitConverter.ToUInt64(buff, 32);
            entry.ending_lba = BitConverter.ToUInt64(buff, 40);
            entry.attributes = BitConverter.ToInt64(buff, 48);

            Buffer.BlockCopy(buff, 56, entry.partition_name, 0, 72);

            return entry;
        }

        public static void get_Da_Info(spdio_t io)
        {
            if (io.raw_len > 6)
            {
                uint sig = BitConverter.ToUInt32(io.big_buf, io.raw_off + 4);
                if (sig == 0x7477656e)
                {
                    int len = 8;
                    while (len + 2 < io.raw_len)
                    {
                        ushort tag = BitConverter.ToUInt16(io.big_buf, io.raw_off + len);
                        ushort size = BitConverter.ToUInt16(io.big_buf, io.raw_off + len + 2);
                        len += 4;

                        switch (tag)
                        {
                            case 0:
                                Da_Info.bDisableHDLC = BitConverter.ToUInt32(io.big_buf, io.raw_off + len);
                                break;
                            case 2:
                                Da_Info.bSupportRawData = io.big_buf[io.raw_off + len];
                                break;
                            case 3:
                                Da_Info.dwFlushSize = BitConverter.ToUInt32(io.big_buf, io.raw_off + len);
                                break;
                            case 6:
                                Da_Info.dwStorageType = BitConverter.ToUInt32(io.big_buf, io.raw_off + len);
                                break;
                        }

                        len += size;
                    }
                }
                else
                {
                    Da_Info = parse_DaInfo(io.big_buf, io.raw_off + 4);
                }
            }

            print("FDL2: incompatible partition");
        }

        public static DA_INFO_T parse_DaInfo(byte[] buffer, int offset)
        {
            if (buffer.Length < offset + 256)
                throw new ArgumentException("Buffer too small.");

            var result = new DA_INFO_T
            {
                bReserve = new byte[2],
                dwReserve = new uint[59]
            };

            result.dwVersion = BitConverter.ToUInt32(buffer, offset + 0);
            result.bDisableHDLC = BitConverter.ToUInt32(buffer, offset + 4);

            result.bIsOldMemory = buffer[offset + 8];
            result.bSupportRawData = buffer[offset + 9];

            Buffer.BlockCopy(buffer, offset + 10, result.bReserve, 0, 2);

            result.dwFlushSize = BitConverter.ToUInt32(buffer, offset + 12);
            result.dwStorageType = BitConverter.ToUInt32(buffer, offset + 16);

            for (int i = 0; i < 59; i++)
            {
                result.dwReserve[i] =
                    BitConverter.ToUInt32(buffer, offset + 20 + i * 4);
            }

            return result;
        }

        public static void select_partition(spdio_t io, string name, ulong size, int mode64, int cmd)
        {
            Span<byte> span = io.big_buf.AsSpan(io.temp_off);
            for (int i = 0; i < 36; i++)
            {
                ushort value = 0;

                if (i < name.Length)
                    value = name[i];

                BitConverter.TryWriteBytes(span.Slice(i * 2, 2), value);
            }

            BitConverter.TryWriteBytes(span.Slice(72, 4), (uint)(size & 0xFFFFFFFF));
            uint size_hi = (mode64 != 0) ? (uint)(size >> 32) : 0;
            BitConverter.TryWriteBytes(span.Slice(76, 4), size_hi);
            BitConverter.TryWriteBytes(span.Slice(80, 8), (ulong)0);
            int payloadLen = 72 + (mode64 != 0 ? 16 : 4);

            encode_msg_nocpy(io, cmd, payloadLen);
        }

        public static void select_ab(spdio_t io)
        {
            bootloader_control? abc = null;
            int ret;

            select_partition(io, "misc", 0x820, 0, (int)BSL_CMD_READ_START);
            if (send_and_check(io) != 0)
            {
                encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
                send_and_check(io);
                selected_ab = 0;
                return;
            }

            int p0 = io.temp_off;
            write32_le(io.big_buf, p0, 0x20);
            write32_le(io.big_buf, p0 + 4, 0x800);

            encode_msg_nocpy(io, (int)BSL_CMD_READ_MIDST, 8);
            send_msg(io);
            ret = recv_msg(io);
            if (ret == 0)
                throw new Exception("timeout reached");

            if (recv_type(io) == (int)BSL_REP_READ_FLASH)
            {
                abc = parse_bootloader_control(io.big_buf, io.raw_off + 4);
            }

            encode_msg_nocpy(io, (int)BSL_CMD_READ_END, 0);
            send_and_check(io);

            if (abc == null) { selected_ab = 0; return; }
            if (abc.Value.nb_slot != 2) { selected_ab = 0; return; }

            if (ab_compare_slots(abc.Value.slot_info[1], abc.Value.slot_info[0]) < 0)
                selected_ab = 2;
            else
                selected_ab = 1;

            if (selected_ab > 0 && check_partition(io, "uboot_a", 0) == 0)
                selected_ab = 0;
        }

        public static int ab_compare_slots(slot_metadata a, slot_metadata b)
        {
            if (a.priority != b.priority)
                return b.priority - a.priority;
            if (a.successful_boot != b.successful_boot)
                return b.successful_boot - a.successful_boot;
            if (a.tries_remaining != b.tries_remaining)
                return b.tries_remaining - a.tries_remaining;
            return 0;
        }

        public static bootloader_control parse_bootloader_control(byte[] buffer, int offset)
        {
            if (buffer.Length < offset + 32)
                throw new ArgumentException("Buffer too small.");

            var result = new bootloader_control
            {
                slot_suffix = new char[4],
                reserved0 = new byte[1],
                slot_info = new slot_metadata[4],
                reserved1 = new byte[8]
            };

            for (int i = 0; i < 4; i++)
                result.slot_suffix[i] = BitConverter.ToChar(buffer, offset + i * 2);

            result.magic = BitConverter.ToUInt32(buffer, offset + 8);
            result.version = buffer[offset + 12];

            byte bitfield1 = buffer[offset + 13];
            result.nb_slot = (byte)(bitfield1 & 0x07);

            byte bitfield2 = buffer[offset + 14];
            result.recovery_tries_remaining = (byte)(bitfield2 & 0x07);

            byte bitfield3 = buffer[offset + 15];
            result.merge_status = (byte)(bitfield3 & 0x07);

            result.reserved0[0] = buffer[offset + 16];

            for (int i = 0; i < 4; i++)
            {
                result.slot_info[i] = new slot_metadata();
                byte data = buffer[offset + 17 + i];

                result.slot_info[i].priority = (byte)(data & 0x0F);
                result.slot_info[i].tries_remaining = (byte)((data >> 4) & 0x07);
                result.slot_info[i].successful_boot = (byte)((data >> 7) & 0x01);
            }

            Buffer.BlockCopy(buffer, offset + 21, result.reserved1, 0, 8);

            result.crc32_le = BitConverter.ToUInt32(buffer, offset + 29);

            return result;
        }

        public static void get_partition_info(spdio_t io, string name, int need_size)
        {
            int verbose = io.verbose;
            io.verbose = 0;

            if (char.IsDigit(name[0]))
            {
                int i = int.Parse(name);
                if (i == 0)
                {
                    gPartInfo.name = "splloader";
                    gPartInfo.size = 256 * 1024;
                    io.verbose = verbose;
                    return;
                }

                if (gpt_failed == 1)
                    io.ptable = partition_list(io, fn_partlist, out io.part_count)!;

                if (i > io.part_count)
                {
                    print("part not exist");
                    gPartInfo.size = 0;
                    io.verbose = verbose;
                    return;
                }

                gPartInfo.name = io.ptable[i - 1].name;
                gPartInfo.size = io.ptable[i - 1].size;
                io.verbose = verbose;
                return;
            }

            if (name.StartsWith("splloader"))
            {
                gPartInfo.name = name;
                gPartInfo.size = 256 * 1024;
                io.verbose = verbose;
                return;
            }

            if (io.part_count > 0)
            {
                string name_ab = name;
                if (selected_ab > 0)
                    name_ab = $"{name}_{(char)(96 + selected_ab)}";

                int i;
                for (i = 0; i < io.part_count; i++)
                {
                    if (name == io.ptable[i].name)
                        break;
                    if (selected_ab > 0 && name_ab == io.ptable[i].name)
                    {
                        name = name_ab;
                        break;
                    }
                }

                if (i < io.part_count)
                {
                    gPartInfo.name = name;
                    gPartInfo.size = io.ptable[i].size;
                }
                else
                {
                    gPartInfo.size = 0;
                }

                io.verbose = verbose;
                return;
            }

            if (selected_ab < 0)
                select_ab(io);

            gPartInfo.size = (long)check_partition(io, name, need_size);
            if (gPartInfo.size == 0 && selected_ab > 0)
            {
                string name_ab = $"{name}_{(char)(96 + selected_ab)}";
                gPartInfo.size = (long)check_partition(io, name_ab, need_size);
                name = name_ab;
            }

            if (gPartInfo.size == 0)
            {
                print("part not exist");
                io.verbose = verbose;
                return;
            }

            gPartInfo.name = name;
            io.verbose = verbose;
        }

        public static bool erase_partition(spdio_t io, string name)
        {
            int timeout0 = io.timeout;
            string name0;
            bool ret = false;

            if (name == "userdata")
            {
                byte[] miscbuf = new byte[0x800];
                Array.Clear(miscbuf, 0, miscbuf.Length);

                Encoding.ASCII.GetBytes("boot-recovery").CopyTo(miscbuf, 0);
                Encoding.ASCII.GetBytes("recovery\n--wipe_data\n").CopyTo(miscbuf, 0x40);

                w_mem_to_part_offset(io, "misc", 0, miscbuf, 0x800, 0x1000);
                return true;
            }
            else if (name == "all")
            {
                io.timeout = 100000;
                select_partition(io, "erase_all", 0xffffffff, 0, (int)BSL_CMD_ERASE_FLASH);
                name0 = "erase_all";
            }
            else
            {
                select_partition(io, name, 0, 0, (int)BSL_CMD_ERASE_FLASH);
                name0 = name;
            }

            if (send_and_check(io) == 0)
            {
                print($"Erase Part Done: {name0}");
                ret = true;
            }

            io.timeout = timeout0;
            return ret;
        }

        public static void w_mem_to_part_offset(spdio_t io, string name, long offset, byte[] mem, long length, uint step)
        {
            get_partition_info(io, name, 1);
            if (gPartInfo.size == 0)
            {
                print("part not exist");
                return;
            }
            else if (gPartInfo.size > 0xffffffff)
            {
                print("part too large");
                return;
            }

            string dfile = $"{name}.bin";
            string fix_fn = !string.IsNullOrEmpty(savepath)
                ? Path.Combine(savepath, dfile)
                : dfile;

            FileStream fi;

            if (offset == 0)
            {
                fi = new FileStream(fix_fn, FileMode.Create, FileAccess.Write);
            }
            else
            {
                ulong dumped = dump_partition(io, gPartInfo.name, 0, (ulong)gPartInfo.size, fix_fn, step);
                if ((ulong)gPartInfo.size != dumped)
                {
                    if (File.Exists(fix_fn))
                        File.Delete(fix_fn);

                    return;
                }
                fi = new FileStream(fix_fn, FileMode.Open, FileAccess.ReadWrite);
            }

            if (fi == null)
                throw new Exception($"fopen {fix_fn} failed");

            fi.Seek(offset, SeekOrigin.Begin);

            fi.Write(mem, 0, (int)length);
            fi.Close();

            load_partition_unify(io, gPartInfo.name, fix_fn, step);
        }

        public static int load_partition_unify(spdio_t io, string name, string fn, uint step)
        {
            string name0 = name;
            string name1;
            uint size0, size1;

            if (name.Contains("fixnv1"))
            {
                load_nv_partition(io, name, fn, 4096);
                return 1;
            }

            if (selected_ab > 0 ||
                Da_Info.dwStorageType == 0x101 ||
                io.part_count == 0 ||
                name.StartsWith("splloader"))
            {
                load_partition(io, name, fn, step);
                return 1;
            }

            if (name0.Length >= 36 - 4)
            {
                load_partition(io, name0, fn, step);
                return 1;
            }

            name1 = name0 + "_bak";
            get_partition_info(io, name1, 1);
            if (gPartInfo.size == 0)
            {
                load_partition(io, name0, fn, step);
                return 1;
            }

            size1 = (uint)gPartInfo.size;
            size0 = (uint)check_partition(io, name0, 1);

            for (int i = 0; i < io.part_count; i++)
            {
                if (name0 == io.ptable[i].name)
                {
                    load_partition_force(io, i, fn, step);
                    break;
                }
            }

            if (size0 == size1)
            {
                if (name0 == "vbmeta")
                {
                    try
                    {
                        using (FileStream fi = new FileStream(fn, FileMode.Open, FileAccess.ReadWrite))
                        {
                            fi.Seek(0x7B, SeekOrigin.Begin);
                            fi.WriteByte(0x00);
                        }
                    }
                    catch (Exception ex)
                    {
                        print($"vbmeta patch failed: {ex.Message}");
                        return 1;
                    }
                }

                load_partition(io, name1, fn, step);
                return 2;
            }

            return 1;
        }

        public static void load_nv_partition(spdio_t io, string name, string fn, uint step)
        {
            ulong offset, rsz;
            uint n;
            int ret;

            ulong len = 0;
            byte[]? mem;

            ushort crc = 0;
            uint cs = 0;

            long file_len = 0;
            mem = loadfile(fn, out file_len, 0);
            if (mem == null)
                throw new Exception($"loadfile(\"{fn}\") failed");

            len = (ulong)file_len;
            byte[] mem0 = mem;
            int mem_p = 0;

            if (BitConverter.ToUInt32(mem, 0) == 0x4e56)
                mem_p += 0x200;

            len = 0;
            len += 4;

            ushort[] tmp = new ushort[2];

            while (true)
            {
                tmp[0] = 0;
                tmp[1] = 0;
                tmp[0] = BitConverter.ToUInt16(mem, mem_p + (int)len + 0);
                tmp[1] = BitConverter.ToUInt16(mem, mem_p + (int)len + 2);

                if (tmp[1] == 0)
                {
                    print("broken NV file, skipping!");
                    return;
                }

                len += 4;
                len += tmp[1];

                uint doffset = (uint)(((len + 3) & 0xFFFFFFFCUL) - len);
                len += doffset;

                if (BitConverter.ToUInt16(mem, mem_p + (int)len) == 0xffff)
                {
                    len += 8;
                    break;
                }
            }

            crc = crc16(crc, mem, mem_p + 2, (int)len - 2);
            write16_be(mem, mem_p, crc);

            for (offset = 0; offset < len; offset++)
                cs += mem[mem_p + (int)offset];

            print($"file size : 0x{len:X}");

            ret = copy_to_wstr(io.big_buf, io.temp_off, 36, name);
            if (ret != 0)
                throw new Exception("name too long");

            write32_le(io.big_buf, io.temp_off + 72, (uint)len);
            write32_le(io.big_buf, io.temp_off + 76, cs);

            encode_msg_nocpy(io, (int)BSL_CMD_START_DATA, 80);

            if (send_and_check(io) != 0)
                return;

            for (offset = 0; (rsz = len - offset) != 0; offset += n)
            {
                n = rsz > step ? step : (uint)rsz;

                Buffer.BlockCopy(mem, mem_p + (int)offset, io.big_buf, io.temp_off, (int)n);

                encode_msg_nocpy(io, (int)BSL_CMD_MIDST_DATA, (int)n);
                send_msg(io);

                ret = recv_msg_timeout(io, 15000);
                if (ret == 0)
                    throw new Exception("timeout reached");

                if ((ret = recv_type(io)) != (int)BSL_REP_ACK)
                {
                    print($"unexpected response (0x{ret:X4})");
                    break;
                }
            }

            encode_msg_nocpy(io, (int)BSL_CMD_END_DATA, 0);
            if (send_and_check(io) == 0)
            {
                print($"Write NV_Part Done: {name}, target: 0x{len:X}, written: 0x{offset:X}");
            }
        }

        public static void load_partition_force(spdio_t io, int id, string fn, uint step)
        {
            int i, j;
            int buf_off = io.temp_off;
            string force_name = "w_force";

            for (i = 0; i < io.part_count; i++)
            {
                Array.Clear(io.big_buf, buf_off, 36 * 2);

                if (i == id)
                {
                    for (j = 0; j < force_name.Length; j++)
                        io.big_buf[buf_off + j * 2] = (byte)force_name[j];
                }
                else
                {
                    string pname = io.ptable[i].name;
                    for (j = 0; j < pname.Length; j++)
                        io.big_buf[buf_off + j * 2] = (byte)pname[j];
                }

                if (j == 0)
                    throw new Exception("empty partition name");

                if (i + 1 == io.part_count)
                    write32_le(io.big_buf, buf_off + 0x48, unchecked((uint)~0));
                else
                    write32_le(io.big_buf, buf_off + 0x48, (uint)(io.ptable[i].size >> 20));

                buf_off += 0x4c;
            }

            encode_msg_nocpy(io, (int)BSL_CMD_REPARTITION, io.part_count * 0x4c);
            if (send_and_check(io) != 0) return;

            load_partition(io, force_name, fn, step);

            buf_off = io.temp_off;
            for (i = 0; i < io.part_count; i++)
            {
                Array.Clear(io.big_buf, buf_off, 36 * 2);

                string pname = io.ptable[i].name;
                for (j = 0; j < pname.Length; j++)
                    io.big_buf[buf_off + j * 2] = (byte)pname[j];

                if (j == 0)
                    throw new Exception("empty partition name");

                if (i + 1 == io.part_count)
                    write32_le(io.big_buf, buf_off + 0x48, unchecked((uint)~0));
                else
                    write32_le(io.big_buf, buf_off + 0x48, (uint)(io.ptable[i].size >> 20));

                buf_off += 0x4c;
            }

            encode_msg_nocpy(io, (int)BSL_CMD_REPARTITION, io.part_count * 0x4c);
            if (send_and_check(io) == 0)
                print($"Force Write {io.ptable[id].name} Done");
        }

        public static void load_partition(spdio_t io, string name, string fn, uint step)
        {
            const uint SPARSE_MAGIC = 0xED26FF3A;
            const ushort CHUNK_RAW = 0xCAC1;
            const ushort CHUNK_FILL = 0xCAC2;
            const ushort CHUNK_DONTCARE = 0xCAC3;
            const ushort CHUNK_CRC32 = 0xCAC4;

            ulong offset = 0, len, n64;
            uint n, step0 = step;
            int ret;

            if (name.Contains("runtimenv"))
            {
                erase_partition(io, name);
                return;
            }
            if (name == "calinv") return;

            using (FileStream fi = new FileStream(fn, FileMode.Open, FileAccess.Read))
            {
                BinaryReader br = new BinaryReader(fi);

                uint magic = br.ReadUInt32();
                bool is_simg = magic == SPARSE_MAGIC;
                fi.Seek(0, SeekOrigin.Begin);

                uint blk_sz = 0, total_chunks = 0;
                if (is_simg)
                {
                    br.ReadUInt32();
                    br.ReadUInt16();
                    br.ReadUInt16();
                    ushort file_hdr_sz = br.ReadUInt16();
                    br.ReadUInt16();
                    blk_sz = br.ReadUInt32();
                    uint total_blks = br.ReadUInt32();
                    total_chunks = br.ReadUInt32();
                    br.ReadUInt32();

                    len = (ulong)total_blks * blk_sz;
                    fi.Seek(file_hdr_sz, SeekOrigin.Begin);
                }
                else
                {
                    fi.Seek(0, SeekOrigin.End);
                    len = (ulong)fi.Position;
                    fi.Seek(0, SeekOrigin.Begin);
                }

                print($"file raw size : 0x{len:X}");

                uint mode64 = (uint)(len >> 32);
                select_partition(io, name, len, (int)mode64, (int)BSL_CMD_START_DATA);
                if (send_and_check(io) != 0) return;

                ulong time_start = GetTickCount64();
                if (is_simg)
                {
                    byte[] zeroBuf = new byte[step];
                    byte[] fillBuf = new byte[step];

                    for (uint c = 0; c < total_chunks; c++)
                    {
                        ushort type = br.ReadUInt16();
                        br.ReadUInt16();
                        uint chunk_sz = br.ReadUInt32();
                        br.ReadUInt32();

                        ulong chunk_bytes = (ulong)chunk_sz * blk_sz;

                        if (type == CHUNK_RAW)
                        {
                            ulong remain = chunk_bytes;
                            while (remain != 0)
                            {
                                n = (uint)Math.Min(step, remain);

                                if (fi.Read(io.big_buf, io.temp_off, (int)n) != n)
                                    throw new Exception("sparse raw read failed");

                                encode_msg_nocpy(io, (int)BSL_CMD_MIDST_DATA, (int)n);
                                send_msg(io);

                                ret = recv_msg_timeout(io, 15000);
                                if (ret == 0)
                                    throw new Exception("timeout reached");
                                if (recv_type(io) != (int)BSL_REP_ACK)
                                    throw new Exception("nack");

                                offset += n;
                                remain -= n;
                                print_progress_bar(offset, len, time_start);
                            }
                        }
                        else if (type == CHUNK_FILL)
                        {
                            uint fill = br.ReadUInt32();

                            for (int i = 0; i < fillBuf.Length; i += 4)
                                write32_le(fillBuf, i, fill);

                            ulong remain = chunk_bytes;
                            while (remain != 0)
                            {
                                n = (uint)Math.Min(step, remain);

                                Buffer.BlockCopy(fillBuf, 0, io.big_buf, io.temp_off, (int)n);

                                encode_msg_nocpy(io, (int)BSL_CMD_MIDST_DATA, (int)n);
                                send_msg(io);

                                ret = recv_msg_timeout(io, 15000);
                                if (ret == 0)
                                    throw new Exception("timeout reached");
                                if (recv_type(io) != (int)BSL_REP_ACK)
                                    throw new Exception("nack");

                                offset += n;
                                remain -= n;
                                print_progress_bar(offset, len, time_start);
                            }
                        }
                        else if (type == CHUNK_DONTCARE)
                        {
                            ulong remain = chunk_bytes;
                            while (remain != 0)
                            {
                                n = (uint)Math.Min(step, remain);

                                Array.Clear(io.big_buf, io.temp_off, (int)n);

                                encode_msg_nocpy(io, (int)BSL_CMD_MIDST_DATA, (int)n);
                                send_msg(io);

                                ret = recv_msg_timeout(io, 15000);
                                if (ret == 0)
                                    throw new Exception("timeout reached");
                                if (recv_type(io) != (int)BSL_REP_ACK)
                                    throw new Exception("nack");

                                offset += n;
                                remain -= n;
                                print_progress_bar(offset, len, time_start);
                            }
                        }
                        else if (type == CHUNK_CRC32)
                        {
                            br.ReadUInt32();
                        }
                        else
                        {
                            throw new Exception($"unknown sparse chunk type 0x{type:X}");
                        }
                    }
                }
                else
                {
                    if (Da_Info.bSupportRawData != 0)
                    {
                        if (Da_Info.bSupportRawData > 1)
                        {
                            encode_msg_nocpy(io, (int)BSL_CMD_MIDST_RAW_START2, 0);
                            if (send_and_check(io) != 0)
                            {
                                Da_Info.bSupportRawData = 0;
                                for (offset = 0; (n64 = len - offset) != 0; offset += n)
                                {
                                    n = (uint)(n64 > step ? step : n64);

                                    if (fi.Read(io.big_buf, io.temp_off, (int)n) != (int)n)
                                        throw new Exception("fread(load) failed");

                                    encode_msg_nocpy(io, (int)BSL_CMD_MIDST_DATA, (int)n);
                                    send_msg(io);

                                    ret = recv_msg_timeout(io, 15000);
                                    if (ret == 0) throw new Exception("timeout reached");
                                    if (recv_type(io) != (int)BSL_REP_ACK) break;

                                    print_progress_bar(offset + n, len, time_start);
                                }
                            }
                        }

                        step = (uint)(Da_Info.dwFlushSize << 10);
                        byte[] rawbuf = new byte[step];

                        for (offset = 0; (n64 = len - offset) != 0; offset += n)
                        {
                            n = (uint)(n64 > step ? step : n64);

                            if (Da_Info.bSupportRawData == 1)
                            {
                                int p = io.temp_off;
                                write32_le(io.big_buf, p, (uint)offset); p += 4;
                                write32_le(io.big_buf, p, (uint)(offset >> 32)); p += 4;
                                write32_le(io.big_buf, p, n);

                                encode_msg_nocpy(io, (int)BSL_CMD_MIDST_RAW_START, 12);
                                if (send_and_check(io) != 0)
                                {
                                    if (offset != 0) break;
                                    step = step0;
                                    Da_Info.bSupportRawData = 0;
                                    continue;
                                }
                            }

                            if (fi.Read(rawbuf, 0, (int)n) != (int)n)
                                throw new Exception("fread(load) failed");

                            ret = call_Write(rawbuf, 0, (int)n);
                            if (ret != (int)n) throw new Exception("usb_send failed");

                            ret = recv_msg_timeout(io, 15000);
                            if (ret == 0) throw new Exception("timeout reached");
                            if (recv_type(io) != (int)BSL_REP_ACK) break;

                            print_progress_bar(offset + n, len, time_start);
                        }
                    }
                    else
                    {
                        for (offset = 0; (n64 = len - offset) != 0; offset += n)
                        {
                            n = (uint)(n64 > step ? step : n64);

                            if (fi.Read(io.big_buf, io.temp_off, (int)n) != (int)n)
                                throw new Exception("fread(load) failed");

                            encode_msg_nocpy(io, (int)BSL_CMD_MIDST_DATA, (int)n);
                            send_msg(io);

                            ret = recv_msg_timeout(io, 15000);
                            if (ret == 0) throw new Exception("timeout reached");
                            if (recv_type(io) != (int)BSL_REP_ACK) break;

                            print_progress_bar(offset + n, len, time_start);
                        }
                    }
                }
            }

            encode_msg_nocpy(io, (int)BSL_CMD_END_DATA, 0);
            if (send_and_check(io) == 0)
                print($"\nWrite Part Done: {name}, target: 0x{len:X}, written: 0x{offset:X}");
        }

        public static bool repartition(spdio_t io, string fn)
        {
            int n = scan_xml_partitions(io, fn, 0xffff);
            encode_msg_nocpy(io, (int)BSL_CMD_REPARTITION, n * 0x4c);

            if (send_and_check(io) == 0)
                return true;
            return false;
        }

        public static int scan_xml_partitions(spdio_t io, string fn, int buf_size)
        {
            string part1 = "Partitions>";
            int part1_len = part1.Length, found = 0, stage = 0;

            if (io.ptable == null)
                io.ptable = new partition_t[128];

            string src;
            try
            {
                src = File.ReadAllText(fn);
            }
            catch
            {
                throw new Exception("loadfile failed");
            }

            int fsize = src.Length;
            int pIndex = 0;

            while (true)
            {
                if (pIndex >= fsize) break;
                char a = src[pIndex++];
                if (char.IsWhiteSpace(a)) continue;

                if (a != '<')
                {
                    if (a == '\0') break;
                    if (stage != 1) continue;
                    throw new Exception("xml: unexpected symbol");
                }

                if (pIndex + 2 < fsize && src.Substring(pIndex, 3) == "!--")
                {
                    int end = src.IndexOf("-->", pIndex + 3, StringComparison.Ordinal);
                    if (end < 0) throw new Exception("xml: unexpected syntax");
                    pIndex = end + 3;
                    continue;
                }

                if (stage != 1)
                {
                    if (pIndex + part1_len <= fsize && src.Substring(pIndex, part1_len) == part1)
                        stage++;
                    if (stage > 2)
                        throw new Exception("xml: more than one partition lists");

                    int close = src.IndexOf('>', pIndex);
                    if (close < 0) throw new Exception("xml: unexpected syntax");
                    pIndex = close + 1;
                    continue;
                }

                if (src[pIndex] == '/' && src.Substring(pIndex + 1, part1_len) == part1)
                {
                    pIndex += 1 + part1_len;
                    stage++;
                    continue;
                }

                int idStart = src.IndexOf("id=\"", pIndex, StringComparison.Ordinal);
                if (idStart < 0) throw new Exception("xml: unexpected syntax");
                idStart += 4;
                int idEnd = src.IndexOf('"', idStart);
                string name = src.Substring(idStart, idEnd - idStart);

                int sizeStart = src.IndexOf("size=\"", idEnd, StringComparison.Ordinal);
                sizeStart += 6;
                int sizeEnd = src.IndexOf('"', sizeStart);
                string sizeStr = src.Substring(sizeStart, sizeEnd - sizeStart);

                long size;
                if (sizeStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    uint tmp = uint.Parse(sizeStr.Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier);
                    size = unchecked((int)tmp);
                }
                else
                {
                    size = long.Parse(sizeStr, System.Globalization.CultureInfo.InvariantCulture);
                }

                int closeTag = src.IndexOf("/>", sizeEnd, StringComparison.Ordinal);
                pIndex = closeTag + 2;

                if (buf_size < 0x4c)
                    throw new Exception("xml: too many partitions");
                buf_size -= 0x4c;

                int baseOff = io.temp_off + found * 0x4c;
                Array.Clear(io.big_buf, baseOff, 36 * 2);
                for (int i = 0; i < name.Length; i++)
                    io.big_buf[baseOff + i * 2] = (byte)name[i];

                if (name.Length == 0)
                    throw new Exception("empty partition name");

                BitConverter.GetBytes((uint)size).CopyTo(io.big_buf, baseOff + 0x48);

                io.ptable[found].name = name;
                io.ptable[found].size = size << 20;

                print($"[{found + 1}] {name}, {size}");
                found++;
            }

            io.part_count = found;

            if (pIndex != fsize)
                throw new Exception("xml: zero byte");
            if (stage != 2)
                throw new Exception("xml: unexpected syntax");

            return found;
        }

        public static void dm_disable(spdio_t io, uint step)
        {
            byte ch = 0x01;
            w_mem_to_part_offset(io, "vbmeta", 0x7B, new byte[] { ch }, 1, step);
        }

        public static void dm_enable(spdio_t io, uint step)
        {
            string[] list = {
                "vbmeta",
                "vbmeta_system",
                "vbmeta_vendor",
                "vbmeta_system_ext",
                "vbmeta_product",
                "vbmeta_odm"
            };

            byte ch = 0x00;

            foreach (string part in list)
            {
                w_mem_to_part_offset(io, part, 0x7B, new byte[] { ch }, 1, step);
            }
        }

        public static bool reboot_device(spdio_t io)
        {
            encode_msg_nocpy(io, (int)BSL_CMD_NORMAL_RESET, 0);
            if (send_and_check(io) != 0) return false;

            return true;
        }
    }
}
