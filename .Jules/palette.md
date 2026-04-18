## 2024-05-18 - WinForms Icon-Only Buttons
**Learning:** WinForms standard Button controls with only visual cues like "..." for browsing are read as literal punctuation by screen readers, creating a confusing experience.
**Action:** Always add `AccessibleName` and `AccessibleDescription` properties to icon-only or punctuation-only WinForms controls.
## 2024-05-19 - Custom Owner-Drawn WinForms Controls Accessibility
**Learning:** When creating custom owner-drawn controls in WinForms by completely overriding `OnPaint` (without calling `base.OnPaint`), the standard Windows keyboard focus cues (dotted rectangle) are lost, making keyboard navigation inaccessible.
**Action:** Always manually restore focus indicators in custom `OnPaint` methods by checking `this.Focused` and `this.ShowFocusCues` and drawing a focus rectangle (e.g., using `ControlPaint.DrawFocusRectangle`).
