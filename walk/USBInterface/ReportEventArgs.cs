using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace USBInterface
{
    // Readonly data that you get from the device
    public class ReportEventArgs : EventArgs
    {
        public ReportEventArgs(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; private set; }
    }
}
