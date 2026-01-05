using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OPTools.Utils;

public static class WindowsApi
{
    // Constants
    public const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_DUP_HANDLE = 0x0040;
    public const uint PROCESS_TERMINATE = 0x0001;
    public const uint DUPLICATE_CLOSE_SOURCE = 0x00000001;
    public const uint DUPLICATE_SAME_ACCESS = 0x00000002;
    public const uint FILE_READ_ATTRIBUTES = 0x0080;
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint FILE_SHARE_DELETE = 0x00000004;
    public const uint OPEN_EXISTING = 3;
    public const uint GENERIC_READ = 0x80000000;
    public const int INVALID_HANDLE_VALUE = -1;

    // System Information Classes
    public enum SYSTEM_INFORMATION_CLASS
    {
        SystemHandleInformation = 16
    }

    // Handle Information Structures
    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_HANDLE
    {
        public uint ProcessId;
        public byte ObjectTypeNumber;
        public byte Flags;
        public ushort Handle;
        public IntPtr Object;
        public uint GrantedAccess;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_HANDLE_INFORMATION
    {
        public uint HandleCount;
        public SYSTEM_HANDLE Handles;
    }

    // Object Name Information
    [StructLayout(LayoutKind.Sequential)]
    public struct OBJECT_NAME_INFORMATION
    {
        public UNICODE_STRING Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    // Object Type Information
    public enum OBJECT_INFORMATION_CLASS
    {
        ObjectNameInformation = 1,
        ObjectTypeInformation = 2
    }

    // NtDll Functions
    [DllImport("ntdll.dll")]
    public static extern uint NtQuerySystemInformation(
        SYSTEM_INFORMATION_CLASS SystemInformationClass,
        IntPtr SystemInformation,
        uint SystemInformationLength,
        out uint ReturnLength);

    [DllImport("ntdll.dll")]
    public static extern uint NtQueryObject(
        IntPtr Handle,
        OBJECT_INFORMATION_CLASS ObjectInformationClass,
        IntPtr ObjectInformation,
        uint ObjectInformationLength,
        out uint ReturnLength);

    [DllImport("ntdll.dll")]
    public static extern uint NtDuplicateObject(
        IntPtr SourceProcessHandle,
        IntPtr SourceHandle,
        IntPtr TargetProcessHandle,
        out IntPtr TargetHandle,
        uint DesiredAccess,
        uint Attributes,
        uint Options);

    // Kernel32 Functions
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        IntPtr hSourceHandle,
        IntPtr hTargetProcessHandle,
        out IntPtr lpTargetHandle,
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwOptions);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool QueryDosDevice(
        string lpDeviceName,
        StringBuilder lpTargetPath,
        uint ucchMax);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetFileInformationByHandleEx(
        IntPtr hFile,
        int FileInformationClass,
        IntPtr lpFileInformation,
        uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool TerminateProcess(
        IntPtr hProcess,
        uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetLastError();

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool MoveFileEx(
        string lpExistingFileName,
        string? lpNewFileName,
        uint dwFlags);

    public const uint MOVEFILE_REPLACE_EXISTING = 0x00000001;
    public const uint MOVEFILE_COPY_ALLOWED = 0x00000002;
    public const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;
    public const uint MOVEFILE_WRITE_THROUGH = 0x00000008;

    // Advapi32 Functions
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        uint DesiredAccess,
        out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool LookupPrivilegeValue(
        string lpSystemName,
        string lpName,
        out long lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public long Luid;
        public uint Attributes;
    }

    public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    public const uint TOKEN_QUERY = 0x0008;
    public const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    public const string SE_DEBUG_NAME = "SeDebugPrivilege";
    public const string SE_BACKUP_NAME = "SeBackupPrivilege";
    public const string SE_RESTORE_NAME = "SeRestorePrivilege";

    // Helper method to enable privileges
    public static bool EnablePrivilege(string privilegeName)
    {
        IntPtr hToken;
        if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 
            TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
        {
            return false;
        }

        try
        {
            if (!LookupPrivilegeValue(string.Empty, privilegeName, out long luid))
            {
                return false;
            }

            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            CloseHandle(hToken);
        }
    }

    // User32 Functions
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

    public const uint WM_DROPFILES = 0x0233;
    public const uint WM_COPYDATA = 0x004A;
    public const uint WM_COPYGLOBALDATA = 0x0049;
    public const uint MSGFLT_ADD = 1;

    // Shell32 Functions
    [DllImport("shell32.dll", SetLastError = true)]
    public static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern void DragFinish(IntPtr hDrop);

    // Restart Manager API - for reliable file lock detection
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames,
        uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, out uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmEndSession(uint pSessionHandle);

    [StructLayout(LayoutKind.Sequential)]
    public struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    public enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    public const int RmRebootReasonNone = 0;
    public const int ERROR_MORE_DATA = 234;
}

