## 2024-05-18 - WinForms Icon-Only Buttons
**Learning:** WinForms standard Button controls with only visual cues like "..." for browsing are read as literal punctuation by screen readers, creating a confusing experience.
**Action:** Always add `AccessibleName` and `AccessibleDescription` properties to icon-only or punctuation-only WinForms controls.
## 2024-05-19 - WinForms Owner-Drawn Controls Focus State
**Learning:** When creating custom owner-drawn controls in WinForms that override `OnPaint` without calling `base.OnPaint`, standard keyboard focus cues are completely lost.
**Action:** Always manually restore focus rectangles using `ControlPaint.DrawFocusRectangle` conditionally based on `this.Focused` and `this.ShowFocusCues` to ensure keyboard accessibility.
