using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OPTools.Core;

public class ApplicationLauncher
{
    private readonly string _configPath;
    private readonly List<string> _shortcuts;

    public ApplicationLauncher()
    {
        _configPath = Path.Combine(Application.StartupPath, "OPToolsApps.Config");
        _shortcuts = new List<string>();
        LoadShortcuts();
    }

    public List<string> GetShortcuts()
    {
        return new List<string>(_shortcuts);
    }

    public void LoadShortcuts()
    {
        _shortcuts.Clear();
        
        if (!File.Exists(_configPath))
        {
            return;
        }

        try
        {
            using (StreamReader reader = new StreamReader(_configPath))
            {
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string path = line.Trim();
                        // Validate that the file still exists
                        if (File.Exists(path) || Directory.Exists(path))
                        {
                            _shortcuts.Add(path);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading shortcuts: {ex.Message}");
        }
    }

    public void SaveShortcuts()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(_configPath, false))
            {
                foreach (string shortcut in _shortcuts)
                {
                    writer.WriteLine(shortcut);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving shortcuts: {ex.Message}");
        }
    }

    public void AddShortcut(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        path = path.Trim();
        
        // Check if already exists
        if (_shortcuts.Contains(path, StringComparer.OrdinalIgnoreCase))
            return;

        // Validate file exists
        if (!File.Exists(path) && !Directory.Exists(path))
            return;

        _shortcuts.Add(path);
        SaveShortcuts();
    }

    public void RemoveShortcut(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _shortcuts.RemoveAll(s => string.Equals(s, path, StringComparison.OrdinalIgnoreCase));
        SaveShortcuts();
    }

    public bool HasShortcut(string path)
    {
        return _shortcuts.Any(s => string.Equals(s, path, StringComparison.OrdinalIgnoreCase));
    }
}

