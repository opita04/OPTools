# Forms - UI Panels and Dialogs

**Purpose:** WinForms UI components with custom modern styling
**Files:** 5+

## OVERVIEW
Panel-based UI components for OPTools features. Uses custom dark theme.

## STRUCTURE
```
Forms/
├── BackupSchedulerPanel.cs     # Backup scheduling UI (889 lines)
├── ContextMenuManagerPanel.cs   # Context menu management UI (671 lines)
├── PackageDetailsPanel.cs      # Package details dialog
├── UpdateDialog.cs             # Update notifications
└── LogsDialog.cs               # Log viewer
```

## WHERE TO LOOK
| Task | File | Notes |
|------|------|-------|
| Backup scheduling | `BackupSchedulerPanel.cs` | Scheduler UI |
| Context menu config | `ContextMenuManagerPanel.cs` | Registry-based menu management |
| Package details | `PackageDetailsPanel.cs` | Detail view for packages |

## CONVENTIONS

### Theme System
```csharp
// Standard dark theme colors
Color cBackground = Color.FromArgb(30, 30, 30);      // #1E1E1E
Color cAccent = Color.FromArgb(0, 122, 204);        // #007ACC
Color cDanger = Color.FromArgb(217, 83, 79);         // #D9534F
Color cText = Color.FromArgb(241, 241, 241);         // #F1F1F1
```

### Custom Controls
- `ModernButton` - Styled button with hover effects
- `SidebarButton` - Navigation buttons in sidebar
- All panels inherit from `Panel` for layout flexibility

### Layout Pattern
```csharp
// Standard panel structure
private Panel _headerPanel = null!;
private Panel _contentPanel = null!;
private ListView _listView = null!;
private ProgressBar _progressBar = null!;
private Label _lblStatus = null!;
```

## ANTI-PATTERNS
- **Inline UI construction**: Consider InitializeComponent pattern for complex panels

## NOTES
- All panels swap into MainForm's `_contentPanel` via sidebar navigation
- Progress bars should be updated via `Invoke` from background threads
- Search/filter should use case-insensitive comparison
