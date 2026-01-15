# Tools - System Utilities

**Purpose:** Self-contained system tools and utilities
**Files:** 6 (925 lines)

## OVERVIEW
Individual system utilities integrated into OPTools sidebar navigation.

## STRUCTURE
```
Tools/
├── PortManager.cs           # Port management (view/kill processes on ports)
├── PortManagerForm.cs       # Port manager UI
├── ProcessKiller.cs         # Kill processes by name/ID
├── SystemCleaner.cs         # System cleanup (temp files, caches)
├── NetworkReset.cs          # Reset network adapters/DNS
└── FolderCleaner.cs         # Force delete folders
```

## WHERE TO LOOK
| Task | File | Notes |
|------|------|-------|
| View/kill ports | `PortManager.cs`, `PortManagerForm.cs` | Uses netstat/powershell |
| Kill processes | `ProcessKiller.cs` | By name or PID |
| Clean temp files | `SystemCleaner.cs` | Cache, temp, prefetch |
| Reset network | `NetworkReset.cs` | Flush DNS, reset adapters |

## CONVENTIONS

### Tool Pattern
Tools are static or simple classes invoked from MainForm sidebar:
```csharp
// In MainForm sidebar button click
private void BtnNetwork_Click(object sender, EventArgs e) {
    NetworkReset.ResetNetwork();
    ShowStatus("Network reset complete");
}
```

### Process Management
- Use `ProcessManager.cs` from Core for process operations
- PortManager uses PowerShell `Get-NetTCPConnection` for port info
- ProcessKiller can kill by name or PID

### Cleanup Operations
- SystemCleaner targets: `%TEMP%`, `Prefetch`, `Recycle Bin`, browser caches
- FolderCleaner uses FileUnlocker for forced deletion

## ANTI-PATTERNS
- **Inconsistent tool interface**: Each tool has different API - consider `ITool` interface
- **UI mixing**: PortManagerForm.cs mixes tool logic with UI - extract

## NOTES
- Tools are launched via sidebar navigation in MainForm
- Each tool should have a corresponding panel or dialog in Forms/
- Network operations require admin privileges
- Some tools use PowerShell commands (PortManager, NetworkReset)
