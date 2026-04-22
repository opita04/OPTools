## 2025-04-14 - Process.GetCurrentProcess() Allocation Overhead
**Learning:** In .NET, `Process.GetCurrentProcess()` does not return a cached instance. It allocates a new `System.Diagnostics.Process` object and makes an underlying native API call every time it is invoked. Inside tight loops (like iterating over thousands of system handles during memory/lock enumeration), this leads to excessive GC pressure and CPU overhead.
**Action:** Always prefer direct P/Invoke calls (`[DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();`) when the process handle or ID is needed inside tight, performance-critical loops in C# code.

## 2025-04-14 - Directory Traverse Materialization
**Learning:** In .NET, `Directory.GetDirectories` and `Directory.GetFiles` force materialization of all discovered items into an array before execution continues, which causes massive spikes in memory allocations and Garbage Collection pressure during traversal of deep/large folder structures.
**Action:** Replace `Directory.GetDirectories` and `Directory.GetFiles` with `Directory.EnumerateDirectories` and `Directory.EnumerateFiles` when traversing large trees to utilize deferred execution (yielding items one by one). Note: Ensure that LINQ operations like `.OrderBy()` aren't used right after `Enumerate*` if deferring materialization is the goal.
