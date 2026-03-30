using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeenicsCardSwipeMonitor
{
    using System.Runtime.InteropServices;

    public static class RfIdeasApi
    {

        [DllImport(@"pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short usbConnect();

        // Disconnects and frees the port
        [DllImport("pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short usbDisconnect();

        // Pulls the current badge ID from the reader's memory
        [DllImport("pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short GetActiveID(byte[] buffer, short maxBufferLength);

        // Forces the reader to beep (great for user feedback)
        [DllImport("pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short BeepNow(byte count, byte beepType);

        // Reads the current device configuration into memory
        [DllImport("pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short ReadCfg();

        // Writes the in-memory configuration back to the device
        [DllImport("pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern short WriteCfg();

        // Sets the buzzer duration on card read (0 = silent)
        [DllImport("pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void SetBuzzerOnDuration(byte duration);

        // Gets the current buzzer-on duration
        [DllImport("pcProxAPI.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern byte GetBuzzerOnDuration();
    }
}
