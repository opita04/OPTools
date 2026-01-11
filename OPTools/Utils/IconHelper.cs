using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using Svg;

namespace OPTools.Utils;

/// <summary>
/// Helper class for loading and rendering SVG icons consistently across the application
/// </summary>
public static class IconHelper
{
    // Icon paths
    private static readonly string IconsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, 
        "Icons");

    // Icon cache - thread-safe
    private static readonly Dictionary<string, Image> IconCache = new();
    private static readonly object CacheLock = new();
    
    // Pre-cached fallback icons for instant return
    private static readonly Dictionary<int, Image> FallbackCache = new();
    
    // Flag to indicate preloading is complete
    private static bool _preloadComplete = false;
    
    // All known icon names for preloading
    private static readonly string[] SidebarIcons = 
    { 
        "Unlocker", "Cleaner", "Network", "Processes", 
        "ContextMenu", "Applications", "Package", "Backup", "Settings" 
    };
    
    private static readonly string[] ActionIcons = 
    { 
        "Add", "Delete", "Edit", "Kill", "Move", 
        "Refresh", "Run", "Save", "Unlock" 
    };

    /// <summary>
    /// Preloads all icons asynchronously. Call this at application startup.
    /// </summary>
    public static async Task PreloadIconsAsync()
    {
        if (_preloadComplete) return;
        
        await Task.Run(() =>
        {
            // Preload sidebar icons (64x64)
            foreach (var icon in SidebarIcons)
            {
                try
                {
                    LoadIconInternal($"icon_{icon.ToLower()}", 64);
                }
                catch { /* Ignore errors during preload */ }
            }
            
            // Preload action icons (24x24)
            foreach (var icon in ActionIcons)
            {
                try
                {
                    LoadIconInternal($"action_{icon.ToLower()}", 24);
                }
                catch { /* Ignore errors during preload */ }
            }
            
            _preloadComplete = true;
        });
    }
    
    /// <summary>
    /// Internal icon loading - thread-safe
    /// </summary>
    private static Image LoadIconInternal(string iconName, int size)
    {
        string cacheKey = $"{iconName}_{size}";
        
        lock (CacheLock)
        {
            if (IconCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }
        
        string iconPath = Path.Combine(IconsPath, $"{iconName}.svg");
        
        if (!File.Exists(iconPath))
        {
            return GetFallbackIcon(size);
        }
        
        try
        {
            var svgDoc = SvgDocument.Open(iconPath);
            var bitmap = svgDoc.Draw(size, size);
            
            lock (CacheLock)
            {
                IconCache[cacheKey] = bitmap;
            }
            
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading icon {iconName}: {ex.Message}");
            return GetFallbackIcon(size);
        }
    }

    /// <summary>
    /// Gets a cached or newly loaded icon from SVG file
    /// </summary>
    public static Image GetIcon(string iconName, int size = 64)
    {
        string cacheKey = $"{iconName}_{size}";
        
        // Fast path: check cache first (most common case after preload)
        lock (CacheLock)
        {
            if (IconCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }
        
        // Cache miss - load synchronously (should be rare after preload)
        return LoadIconInternal(iconName, size);
    }

    /// <summary>
    /// Gets sidebar icons (64x64)
    /// </summary>
    public static Image GetSidebarIcon(string feature)
    {
        return GetIcon($"icon_{feature.ToLower()}", 64);
    }

    /// <summary>
    /// Gets action icons (24x24)
    /// </summary>
    public static Image GetActionIcon(string action)
    {
        return GetIcon($"action_{action.ToLower()}", 24);
    }
    
    /// <summary>
    /// Gets a cached fallback icon - very fast
    /// </summary>
    private static Image GetFallbackIcon(int size)
    {
        lock (FallbackCache)
        {
            if (FallbackCache.TryGetValue(size, out var fallback))
            {
                return fallback;
            }
            
            var icon = CreateFallbackIcon(size);
            FallbackCache[size] = icon;
            return icon;
        }
    }

    /// <summary>
    /// Creates a fallback placeholder icon if loading fails
    /// </summary>
    private static Image CreateFallbackIcon(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        
        // Draw rounded rect with question mark
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(45, 45, 48));
        using var pen = new Pen(Color.FromArgb(150, 150, 150), 2);
        
        var rect = new Rectangle(2, 2, size - 4, size - 4);
        var path = GetRoundedPath(rect, 8);
        
        g.FillPath(brush, path);
        g.DrawPath(pen, path);

        // Draw question mark
        using var textBrush = new SolidBrush(Color.FromArgb(150, 150, 150));
        using var font = new Font("Segoe UI", size / 3, FontStyle.Bold);
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        
        g.DrawString("?", font, textBrush, 
            new Rectangle(0, 0, size, size), format);

        return bitmap;
    }

    /// <summary>
    /// Creates a rounded rectangle path
    /// </summary>
    private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        float r = radius;
        path.AddArc(rect.X, rect.Y, r, r, 180, 90);
        path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>
    /// Clears icon cache (call when theme changes or if icons need refreshing)
    /// </summary>
    public static void ClearCache()
    {
        lock (CacheLock)
        {
            foreach (var kvp in IconCache)
            {
                kvp.Value?.Dispose();
            }
            IconCache.Clear();
        }
        
        lock (FallbackCache)
        {
            foreach (var kvp in FallbackCache)
            {
                kvp.Value?.Dispose();
            }
            FallbackCache.Clear();
        }
        
        _preloadComplete = false;
    }
}
