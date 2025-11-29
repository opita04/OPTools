using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OPTools.Utils;

public class ModernButton : Button
{
    public string IconChar { get; set; } = "";
    public int BorderRadius { get; set; } = 20;
    public Color CornerBackColor { get; set; } = Color.FromArgb(30, 30, 30); // Default to App Background
    public Color HoverColor { get; set; } = Color.Empty;
    
    private Color _originalBackColor;

    public ModernButton()
    {
        this.FlatStyle = FlatStyle.Flat;
        this.FlatAppearance.BorderSize = 0;
        this.Cursor = Cursors.Hand;
        this.Size = new Size(140, 40);
        this.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        this.ForeColor = Color.White;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _originalBackColor = this.BackColor;
        if (HoverColor != Color.Empty)
            this.BackColor = HoverColor;
        else
            this.BackColor = ControlPaint.Light(this.BackColor, 0.1f);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        this.BackColor = _originalBackColor;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Clear background with the corner color (simulating transparency)
        using (var brush = new SolidBrush(CornerBackColor))
        {
            graphics.FillRectangle(brush, this.ClientRectangle);
        }

        // Draw rounded background
        using (var path = GetRoundedPath(this.ClientRectangle, BorderRadius))
        using (var brush = new SolidBrush(this.BackColor))
        {
            graphics.FillPath(brush, path);
        }

        // Draw Icon and Text
        // Icon font: Segoe MDL2 Assets
        // Text font: Segoe UI (from property)

        int iconSize = 12;
        int spacing = 8;
        
        // Measure text
        SizeF textSize = graphics.MeasureString(this.Text, this.Font);
        
        // Calculate total width to center content
        float totalWidth = textSize.Width;
        if (!string.IsNullOrEmpty(IconChar))
            totalWidth += iconSize + spacing;

        float startX = (this.Width - totalWidth) / 2;
        float centerY = this.Height / 2;

        // Draw Icon
        if (!string.IsNullOrEmpty(IconChar))
        {
            using (var iconFont = new Font("Segoe MDL2 Assets", iconSize))
            using (var brush = new SolidBrush(this.ForeColor))
            {
                float iconY = centerY - (iconSize / 2) - 1; // Slight adjustment
                graphics.DrawString(IconChar, iconFont, brush, startX, iconY);
            }
            startX += iconSize + spacing;
        }

        // Draw Text
        using (var brush = new SolidBrush(this.ForeColor))
        {
            float textY = centerY - (textSize.Height / 2);
            graphics.DrawString(this.Text, this.Font, brush, startX, textY);
        }
    }

    private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        float r = radius;
        path.AddArc(rect.X, rect.Y, r, r, 180, 90);
        path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public class SidebarButton : Button
{
    public string IconChar { get; set; } = "";
    public bool IsActive { get; set; } = false;
    public Color ActiveColor { get; set; } = Color.FromArgb(0, 122, 204); // Blue
    public Color InactiveColor { get; set; } = Color.Transparent;
    public Color ActiveTextColor { get; set; } = Color.White;
    public Color InactiveTextColor { get; set; } = Color.FromArgb(150, 150, 150);

    public SidebarButton()
    {
        this.FlatStyle = FlatStyle.Flat;
        this.FlatAppearance.BorderSize = 0;
        this.Cursor = Cursors.Hand;
        this.Size = new Size(200, 45);
        this.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        this.TextAlign = ContentAlignment.MiddleLeft;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background
        Color bg = IsActive ? ActiveColor : InactiveColor;
        Color fg = IsActive ? ActiveTextColor : InactiveTextColor;
        
        // Clear
        using (var brush = new SolidBrush(this.Parent?.BackColor ?? Color.Black))
        {
            graphics.FillRectangle(brush, this.ClientRectangle);
        }

        // Draw rounded rect if active
        if (IsActive)
        {
            // The image shows the active button as a rounded pill shape
            Rectangle rect = new Rectangle(10, 2, this.Width - 20, this.Height - 4);
            using (var path = GetRoundedPath(rect, 15))
            using (var brush = new SolidBrush(bg))
            {
                graphics.FillPath(brush, path);
            }
        }

        // Draw Icon and Text
        int iconX = 25;
        int textX = 55;
        int centerY = this.Height / 2;

        // Icon
        if (!string.IsNullOrEmpty(IconChar))
        {
            using (var iconFont = new Font("Segoe MDL2 Assets", 12))
            using (var brush = new SolidBrush(fg))
            {
                // Center vertically
                float iconY = centerY - 8; 
                graphics.DrawString(IconChar, iconFont, brush, iconX, iconY);
            }
        }

        // Text
        using (var brush = new SolidBrush(fg))
        {
            // Use Bold if active
            Font fontToUse = IsActive ? new Font(this.Font, FontStyle.Bold) : this.Font;
            SizeF textSize = graphics.MeasureString(this.Text, fontToUse);
            float textY = centerY - (textSize.Height / 2);
            graphics.DrawString(this.Text, fontToUse, brush, textX, textY);
        }
    }

    private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        float r = radius;
        path.AddArc(rect.X, rect.Y, r, r, 180, 90);
        path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }
}
