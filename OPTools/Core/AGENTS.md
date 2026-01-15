# Core - Business Logic and Data Models

**Purpose:** Core business logic for file operations, package management
**Files:** 10 (3,752 lines)

## OVERVIEW
Core abstractions for file unlocking, package management, and system operations.

## STRUCTURE
```
Core/
├── FileUnlocker.cs           # Force delete locked files (482 lines)
├── HandleEnumerator.cs        # Process handle enumeration (NtQuerySystemInformation)
├── PackageScanner.cs          # Discover packages in projects (1126 lines)
├── PackageUpdater.cs          # Check package updates
├── PackageDatabase.cs        # SQLite persistence (476 lines)
├── PackageModels.cs          # Data models (PackageInfo, Ecosystem enum)
├── ApplicationLauncher.cs    # Launch applications from DB
├── BackupScheduler.cs        # Backup job scheduling
├── BackupJob.cs              # Backup job model
└── ProcessManager.cs         # Process management utilities
```

## WHERE TO LOOK
| Task | File | Notes |
|------|------|-------|
| Unlock files | `FileUnlocker.cs` | Uses HandleEnumerator + P/Invoke |
| Enumerate handles | `HandleEnumerator.cs` | NtQuerySystemInformation |
| Scan for packages | `PackageScanner.cs` | Multi-ecosystem (NPM, Python, C++, Bun) |
| Package persistence | `PackageDatabase.cs` | SQLite via Microsoft.Data.Sqlite |
| Data models | `PackageModels.cs` | PackageInfo, Ecosystem enum |
| Backup scheduling | `BackupScheduler.cs` | Job scheduling logic |

## CONVENTIONS

### File Unlocking
```csharp
var unlocker = new FileUnlocker(targetPath);
UnlockResult result = unlocker.UnlockAll(killProcesses: true);
if (result.Success) { unlocker.DeleteFileOrFolder(); }
```

### Package Models
```csharp
// Ecosystem types
enum Ecosystem { NPM, Python, Cpp, Bun }

// Package info
class PackageInfo {
    string Name, Version, Path, ProjectPath;
    bool IsOutdated;
    string? LatestVersion, Description, Author;
    // ... many metadata fields
}
```

### Async Operations
- I/O operations use async/await
- Scanner updates progress via callbacks
- Database operations use async SQLite API

## ANTI-PATTERNS
- **P/Invoke scattered**: HandleEnumerator uses NtQuerySystemInformation - centralize in Utils/WindowsApi.cs
- **Large models**: PackageInfo has 30+ properties - consider grouping metadata

## NOTES

### Handle Enumeration
- Requires **admin privileges** (NtQuerySystemInformation is privileged)
- Lists all processes holding handles to target path
- Can kill processes holding locks (via ProcessManager)

### Reserved Filenames
- Windows reserved names (CON, PRN, AUX, NUL, COM1-9, LPT1-9) blocked
- Extended-length paths (`\\?\`) used to bypass reserved name parsing

### Package Ecosystems
- **NPM**: package.json parsing
- **Python**: requirements.txt, pyproject.toml, Poetry lock files
- **Cpp**: vcpkg.json, conanfile.txt
- **Bun**: bun.lockb (binary lock file)
