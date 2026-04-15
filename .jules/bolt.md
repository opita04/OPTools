## 2024-04-15 - Directory Enumeration Optimization
**Learning:** `Directory.GetDirectories()` and `Directory.GetFiles()` allocate large string arrays for all matches before returning, leading to high memory pressure when scanning deep repository trees.
**Action:** Use `Directory.EnumerateDirectories()` and `Directory.EnumerateFiles().FirstOrDefault()` for lazy evaluation, reducing both memory allocations and disk I/O when only the first result is needed.
