# OPTools - Windows System Tools

**Generated:** 2026-01-14
**Framework:** .NET 8.0 (WinForms)
**Lines of Code:** ~14,653 C#

## OVERVIEW
Windows desktop app for file unlocking, package management, backup scheduling, and system utilities.

## STRUCTURE
```
OPTools/
├── Core/          # Business logic (10 files, 3.7k lines)
├── Forms/         # UI panels/dialogs (6 files, 4.7k lines)
├── Tools/         # System utilities (6 files, 925 lines)
├── Registry/      # Registry operations (3 files, 807 lines)
├── Utils/         # Shared utilities (7 files, 1.2k lines)
├── Icons/         # SVG/ICO resources
└── Program.cs     # Entry point
```

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| File unlocking logic | `Core/FileUnlocker.cs` | Uses HandleEnumerator |
| Package management | `Core/PackageScanner.cs`, `Core/PackageUpdater.cs` | Multi-ecosystem (NPM, Python, C++, Bun) |
| UI panels | `Forms/PackageHandlerPanel.cs` | Package management UI (1970 lines) |
| Main form | `MainForm.cs` | Entry point, sidebar navigation (2122 lines) |
| Registry operations | `Registry/ContextMenuInstaller.cs` | Context menu integration |
| Settings | `Utils/SettingsManager.cs` | App persistence |
| Tests | `OPTools.Tests/` | xUnit tests |

## CONVENTIONS

### Architecture
- **WinForms UI** with custom modern controls (`ModernButton`, `SidebarButton`)
- **Sidebar navigation** in MainForm with panel swapping
- **Theme colors**: Dark theme (`#1E1E1E` background, `#007ACC` accent)
- **Async/await**: Used for I/O operations (15 files use async)
- **Registry access**: Via `Microsoft.Win32.Registry` (4 files)

### File Organization
- `MainForm.cs` - Main form with sidebar and content panels
- `MainForm.ContextMenu.cs` - Context menu handling
- `MainForm.DragDrop.cs` - Drag & drop functionality
- Forms inherit from `Panel` for reuse
- Tools in `Tools/` are self-contained utilities

### Security
- **Dangerous paths blocked**: `Utils/PathHelper.IsDangerousPath()` protects system paths
- **Admin privileges**: Required for handle enumeration and context menu install
- **Reserved filenames**: `FileUnlocker` handles Windows reserved names (CON, PRN, etc.)

## ANTI-PATTERNS

- **Large files**: MainForm.cs (2122 lines), PackageHandlerPanel.cs (1970 lines) - consider splitting
- **Empty catch blocks**: Search for `catch` without logging - AuditLogger available
- **Direct Win32 calls**: P/Invoke scattered - centralize in `Utils/WindowsApi.cs`

## COMMANDS

```bash
# Build
dotnet build -c Release

# Publish (self-contained)
dotnet publish -c Release -r win-x64 --self-contained true

# Run tests
dotnet test

# Silent force delete (requires admin)
OPTools.exe "C:\locked\file.txt" /S

# Install context menu (requires admin)
OPTools.exe /INSTALL
```

## NOTES

- **Context menu**: Integrates with Windows Explorer for right-click "Delete with FolderDelete"
- **Self-contained**: Build includes .NET 8.0 runtime
- **Logging**: Serilog configured via `Utils/AuditLogger.cs`
- **Package database**: SQLite via `Microsoft.Data.Sqlite`
- **Multi-language**: Supports 12 languages (zh-Hans, zh-Hant, tr, ru, pt-BR, pl, ko, ja, it, fr, es, de, cs)

### Gotchas
- Handle enumeration requires admin privileges (NtQuerySystemInformation)
- Reserved filenames require extended-length path prefix (`\\?\`)
- Drag & drop paths need quote trimming: `Path.GetFullPath(path.Trim('"'))`
- Silent mode exits with code 2 for dangerous paths, 1 for errors
