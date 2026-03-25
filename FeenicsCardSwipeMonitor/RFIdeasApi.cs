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
    }
}
