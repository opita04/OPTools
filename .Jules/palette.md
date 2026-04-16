## 2024-05-18 - WinForms Icon-Only Buttons
**Learning:** WinForms standard Button controls with only visual cues like "..." for browsing are read as literal punctuation by screen readers, creating a confusing experience.
**Action:** Always add `AccessibleName` and `AccessibleDescription` properties to icon-only or punctuation-only WinForms controls.## 2024-11-20 - Adding Accessibility descriptors and Tooltips to WinForms controls
**Learning:** For C# WinForms applications, accessibility is handled via `AccessibleName` and `AccessibleDescription` properties, serving a similar function to `aria-label` in web dev. Adding visual feedback via a `ToolTip` component handles visual user-experience enhancements.
**Action:** Always add both accessibility descriptors and visual tooltips for custom icon-only controls created dynamically.
