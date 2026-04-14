using System;
using System.Runtime.InteropServices;

namespace OPTools.Utils
{
    public static partial class WindowsApi
    {
        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", ExactSpelling = true)]
        public static extern uint GetCurrentProcessId();
    }
}
