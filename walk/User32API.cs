using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace walk
{
    public class User32API
    {
        [DllImport("User32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_RESTORE = 9;
    }
}

