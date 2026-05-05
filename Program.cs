using Spreadtrum.Library;
using System.Drawing;
using static Spreadtrum.Library.spd_utils;
using static Spreadtrum.Library.spd_channel;
using static Spreadtrum.Library.spdio_t;
using static Spreadtrum.Library.spd_state;
using static Spreadtrum.Library.spd_main;

namespace Spreadtrum
{
    internal class Program
    {
        private static spdio_t io = new spdio_t();

        static void Main(string[] args)
        {
            try
            {
                SendLog("Waiting for device... ", clear: true);
                Device device = UsbWatcher.FindDeviceByName("U2S", 60000);
                SendLog("Okay", Color.Green);
                SendLog("Connected port: ", null, true);
                SendLog(device.Name!, Color.Blue);

                io = spd_init(device);
                spd_handshake(io);
                SendLog("Send loader [1]... ", null, true);
                send_file(io, "fdl1.bin", 0x5000, 1, 528, 0, 0);
                SendLog("Okay", Color.Green);

                SendLog("Send payload data... ", null, true);
                send_file(io, "payload.bin", 0x4ee8, 0, 528, 0, 0);
                SendLog("Okay", Color.Green);
                spd_rehandshake(io, 0x5000);
                if (fdl1_loaded != 1)
                    throw new Exception("Failed to enter stage 1.");

                SendLog("Send loader [2]... ", null, true);
                send_file(io, "fdl2.bin", 0x9efffe00, 1, 528, 0, 0);
                SendLog("Okay", Color.Green);

                read_gpt_da_info(io);
                if (fdl2_executed != 1)
                    throw new Exception("Failed to enter stage 2.");
                if (gpt_failed != 0)
                    throw new Exception("Failed to read gpt info.");

                SendLog("Reboot... ", null, true);
                reboot_device(io);
                SendLog("Okay", Color.Green);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                call_Uninitialize();
            }
            Console.ReadLine();
        }
    }
}
