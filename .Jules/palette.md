## 2024-05-18 - WinForms Icon-Only Buttons
**Learning:** WinForms standard Button controls with only visual cues like "..." for browsing are read as literal punctuation by screen readers, creating a confusing experience.
**Action:** Always add `AccessibleName` and `AccessibleDescription` properties to icon-only or punctuation-only WinForms controls.
## 2026-04-22 - Focus cues in owner-drawn WinForms controls
**Learning:** Overriding OnPaint without calling base.OnPaint drops standard focus cues in custom buttons, hurting keyboard accessibility.
**Action:** Manually restore them using ControlPaint.DrawFocusRectangle conditionally based on this.Focused and this.ShowFocusCues.
