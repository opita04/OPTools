## 2024-05-18 - WinForms Icon-Only Buttons
**Learning:** WinForms standard Button controls with only visual cues like "..." for browsing are read as literal punctuation by screen readers, creating a confusing experience.
**Action:** Always add `AccessibleName` and `AccessibleDescription` properties to icon-only or punctuation-only WinForms controls.## 2024-05-18 - Restoring Keyboard Focus in Owner-Drawn WinForms Controls
**Learning:** When creating custom owner-drawn controls in WinForms (like `ModernButton` and `SidebarButton`) that completely override `OnPaint` without calling `base.OnPaint`, standard keyboard focus visual cues are entirely lost. This severely impacts keyboard accessibility.
**Action:** Always manually restore focus cues in custom `OnPaint` methods by calling `ControlPaint.DrawFocusRectangle`, but ensure it is conditionally drawn based on `this.Focused && this.ShowFocusCues` to respect user OS accessibility settings regarding focus visibility.
