## 2025-04-14 - Process.GetCurrentProcess() Allocation Overhead
**Learning:** In .NET, `Process.GetCurrentProcess()` does not return a cached instance. It allocates a new `System.Diagnostics.Process` object and makes an underlying native API call every time it is invoked. Inside tight loops (like iterating over thousands of system handles during memory/lock enumeration), this leads to excessive GC pressure and CPU overhead.
**Action:** Always prefer direct P/Invoke calls (`[DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();`) when the process handle or ID is needed inside tight, performance-critical loops in C# code.

## 2026-04-19 - Directory.GetDirectories() and Directory.GetFiles() Memory Pressure
**Learning:** Using `Directory.GetDirectories()` or `Directory.GetFiles()` allocates an entire array of strings containing all results. In deep, recursive directory traversal or folders with many entries, this creates significant GC pressure and memory spikes.
**Action:** Always prefer `Directory.EnumerateDirectories()` and `Directory.EnumerateFiles()` for traversal tasks, which stream results lazily and prevent large allocations.
