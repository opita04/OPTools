## 2024-05-18 - Added Keyboard Focus Cues for Modern WinForms Controls
**Learning:** Custom owner-drawn controls in WinForms lose standard keyboard focus cues when overriding `OnPaint` without calling `base.OnPaint`.
**Action:** Always manually restore focus cues using `ControlPaint.DrawFocusRectangle` conditionally based on `this.Focused` and `this.ShowFocusCues` for custom controls.
