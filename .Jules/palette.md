## 2024-05-18 - WinForms Icon-Only Buttons
**Learning:** WinForms standard Button controls with only visual cues like "..." for browsing are read as literal punctuation by screen readers, creating a confusing experience.
**Action:** Always add `AccessibleName` and `AccessibleDescription` properties to icon-only or punctuation-only WinForms controls.## 2024-05-24 - Accessibility for Icon-Only WinForms Buttons
**Learning:** WinForms applications often rely heavily on icon-only buttons (`ModernButton` with `IconChar` or `Image`), which are inherently inaccessible to screen readers and confusing to sighted users without explicit tooltips.
**Action:** Routinely add `AccessibleName`, `AccessibleDescription`, and a `ToolTip` to any newly created or existing icon-only buttons to ensure parity with web ARIA standards.
