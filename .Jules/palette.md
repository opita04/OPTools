## 2024-05-18 - WinForms Icon-Only Buttons
**Learning:** WinForms standard Button controls with only visual cues like "..." for browsing are read as literal punctuation by screen readers, creating a confusing experience.
**Action:** Always add `AccessibleName` and `AccessibleDescription` properties to icon-only or punctuation-only WinForms controls.
## 2024-05-18 - Owner-Drawn WinForms Controls Focus Cues
**Learning:** Overriding `OnPaint` on WinForms controls completely disables standard keyboard focus visualization (dotted rectangles), making custom components invisible to keyboard users.
**Action:** Always manually render focus cues inside custom `OnPaint` methods using `ControlPaint.DrawFocusRectangle(graphics, this.ClientRectangle)` conditionally checking `this.Focused && this.ShowFocusCues`.
