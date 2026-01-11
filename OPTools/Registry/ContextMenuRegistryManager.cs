using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace OPTools.Registry;

public class ContextMenuRegistryManager
{
    // Registry paths for different context menu types
    private const string FolderShell = @"Directory\shell";
    private const string FolderBackgroundShell = @"Directory\Background\shell";
    private const string FileShell = @"*\shell";
    
    private readonly RegistryKey _rootKey = Microsoft.Win32.Registry.ClassesRoot;
    
    private readonly Dictionary<string, string> _contextTypes = new()
    {
        { "Folder", FolderShell },
        { "Folder Background", FolderBackgroundShell },
        { "File", FileShell }
    };

    public List<ContextMenuEntry> ListEntries()
    {
        var entries = new List<ContextMenuEntry>();
        
        foreach (var kvp in _contextTypes)
        {
            string menuType = kvp.Key;
            string regPath = kvp.Value;
            
            try
            {
                using (var shellKey = _rootKey.OpenSubKey(regPath, false))
                {
                    if (shellKey == null)
                        continue;
                    
                    string[] subKeyNames = shellKey.GetSubKeyNames();
                    
                    foreach (string entryName in subKeyNames)
                    {
                        var entryInfo = GetEntryInfo(regPath, entryName, menuType);
                        if (entryInfo != null)
                        {
                            entries.Add(entryInfo);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Permission denied, skip this menu type
                continue;
            }
            catch (Exception)
            {
                // Other errors, skip this menu type
                continue;
            }
        }
        
        return entries;
    }

    private ContextMenuEntry? GetEntryInfo(string shellPath, string entryName, string menuType)
    {
        try
        {
            string entryKeyPath = $"{shellPath}\\{entryName}";
            using (var entryKey = _rootKey.OpenSubKey(entryKeyPath, false))
            {
                if (entryKey == null)
                    return null;
                
                // Get display name (default value of entry key)
                string displayName = entryKey.GetValue("") as string ?? entryName;
                
                // Get command path
                string? commandPath = null;
                using (var commandKey = entryKey.OpenSubKey("command", false))
                {
                    if (commandKey != null)
                    {
                        commandPath = commandKey.GetValue("") as string;
                    }
                }
                
                // Only return if we have a valid command path
                if (!string.IsNullOrEmpty(commandPath))
                {
                    // Extract just the executable path (remove "%1" parameter if present)
                    commandPath = commandPath.Trim('"');
                    if (commandPath.EndsWith(" \"%1\""))
                    {
                        commandPath = commandPath.Substring(0, commandPath.Length - 5);
                    }
                    else if (commandPath.EndsWith(" %1"))
                    {
                        commandPath = commandPath.Substring(0, commandPath.Length - 3);
                    }
                    commandPath = commandPath.Trim('"');
                    
                    return new ContextMenuEntry
                    {
                        Name = entryName,
                        DisplayName = displayName,
                        Command = commandPath,
                        MenuType = menuType,
                        RegistryPath = entryKeyPath
                    };
                }
            }
        }
        catch (Exception)
        {
            // Error getting info, skip this entry
        }
        
        return null;
    }

    public (bool Success, string Message) AddEntry(string appPath, string menuName, List<string> menuTypes)
    {
        // Validate app path
        if (string.IsNullOrWhiteSpace(appPath))
        {
            return (false, "Application path cannot be empty");
        }
        
        // Extract executable path for validation (handles full commands with arguments)
        string executablePath = ExtractExecutablePath(appPath);
        bool isCustomCommand = appPath.Trim() != executablePath; // Has arguments
        
        if (!File.Exists(executablePath))
        {
            return (false, $"Application path does not exist: {executablePath}");
        }
        
        if (Directory.Exists(executablePath))
        {
            return (false, $"Path is a directory, not a file: {executablePath}");
        }
        
        // Validate menu name
        if (string.IsNullOrWhiteSpace(menuName))
        {
            return (false, "Menu name cannot be empty");
        }
        
        // Sanitize menu name for registry key
        string regKeyName = SanitizeKeyName(menuName);
        
        // Normalize app path
        appPath = Path.GetFullPath(appPath);
        
        var errors = new List<string>();
        var successes = new List<string>();
        
        foreach (string menuType in menuTypes)
        {
            if (!_contextTypes.ContainsKey(menuType))
            {
                errors.Add($"Invalid menu type: {menuType}");
                continue;
            }
            
            try
            {
                string shellPath = _contextTypes[menuType];
                string entryKeyPath = $"{shellPath}\\{regKeyName}";
                
                // Create the entry key
                using (var entryKey = _rootKey.CreateSubKey(entryKeyPath, true))
                {
                    if (entryKey != null)
                    {
                        // Set display name
                        entryKey.SetValue("", menuName);
                        
                        // Create command subkey
                        string commandKeyPath = $"{entryKeyPath}\\command";
                        using (var commandKey = _rootKey.CreateSubKey(commandKeyPath, true))
                        {
                            if (commandKey != null)
                            {
                                // Set command path
                                // If user provided a custom command with arguments, use it as-is
                                // Otherwise, build the command based on menu type
                                string commandValue;
                                if (isCustomCommand)
                                {
                                    // User provided full command with arguments - use as-is
                                    commandValue = appPath;
                                }
                                else if (menuType == "Folder Background")
                                {
                                    // No parameter for background context menu
                                    commandValue = appPath.Contains(" ") ? $"\"{appPath}\"" : appPath;
                                }
                                else
                                {
                                    // Folder and File context menus receive "%1" parameter
                                    commandValue = appPath.Contains(" ") 
                                        ? $"\"{appPath}\" \"%1\"" 
                                        : $"{appPath} \"%1\"";
                                }
                                
                                commandKey.SetValue("", commandValue);
                            }
                        }
                    }
                }
                
                successes.Add(menuType);
            }
            catch (UnauthorizedAccessException)
            {
                errors.Add($"Permission denied for {menuType} (run as administrator)");
            }
            catch (Exception ex)
            {
                errors.Add($"Error adding to {menuType}: {ex.Message}");
            }
        }
        
        if (successes.Count > 0 && errors.Count == 0)
        {
            return (true, $"Successfully added to {string.Join(", ", successes)}");
        }
        else if (successes.Count > 0 && errors.Count > 0)
        {
            return (false, $"Partial success: {string.Join(", ", successes)}. Errors: {string.Join("; ", errors)}");
        }
        else
        {
            return (false, $"Failed to add entry: {string.Join("; ", errors)}");
        }
    }

    public (bool Success, string Message) UpdateEntry(string registryPath, string newDisplayName, string newAppPath, string menuType)
    {
        try
        {
            // Validate new app path
            if (string.IsNullOrWhiteSpace(newAppPath))
            {
                return (false, "Application path cannot be empty");
            }
            
            // Extract executable path for validation (handles full commands with arguments)
            string executablePath = ExtractExecutablePath(newAppPath);
            bool isCustomCommand = newAppPath.Trim() != executablePath; // Has arguments
            
            if (!File.Exists(executablePath))
            {
                return (false, $"Application path does not exist: {executablePath}");
            }
            
            // Validate display name
            if (string.IsNullOrWhiteSpace(newDisplayName))
            {
                return (false, "Menu name cannot be empty");
            }
            
            // Open the existing entry key
            using (var entryKey = _rootKey.OpenSubKey(registryPath, true))
            {
                if (entryKey == null)
                {
                    return (false, "Entry not found in registry");
                }
                
                // Update display name
                entryKey.SetValue("", newDisplayName);
                
                // Update command
                using (var commandKey = entryKey.OpenSubKey("command", true))
                {
                    if (commandKey != null)
                    {
                        // Build command value based on menu type
                        // If user provided a custom command with arguments, use it as-is
                        string commandValue;
                        if (isCustomCommand)
                        {
                            // User provided full command with arguments - use as-is
                            commandValue = newAppPath;
                        }
                        else if (menuType == "Folder Background")
                        {
                            commandValue = newAppPath.Contains(" ") ? $"\"{newAppPath}\"" : newAppPath;
                        }
                        else
                        {
                            commandValue = newAppPath.Contains(" ") 
                                ? $"\"{newAppPath}\" \"%1\"" 
                                : $"{newAppPath} \"%1\"";
                        }
                        
                        commandKey.SetValue("", commandValue);
                    }
                    else
                    {
                        return (false, "Command key not found");
                    }
                }
            }
            
            return (true, "Entry updated successfully");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Permission denied (run as administrator)");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating entry: {ex.Message}");
        }
    }

    public (bool Success, string Message) DeleteEntry(string registryPath)
    {
        try
        {
            // Delete the entire key tree
            DeleteKeyRecursive(_rootKey, registryPath);
            return (true, "Entry deleted successfully");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Permission denied (run as administrator)");
        }
        catch (ArgumentException)
        {
            return (false, "Entry not found (may have been already deleted)");
        }
        catch (Exception ex)
        {
            return (false, $"Error deleting entry: {ex.Message}");
        }
    }

    private void DeleteKeyRecursive(RegistryKey rootKey, string keyPath)
    {
        try
        {
            // Open the key to enumerate subkeys
            using (var key = rootKey.OpenSubKey(keyPath, true))
            {
                if (key != null)
                {
                    // Delete all subkeys first
                    string[] subKeyNames = key.GetSubKeyNames();
                    foreach (string subKeyName in subKeyNames)
                    {
                        string subKeyPath = $"{keyPath}\\{subKeyName}";
                        DeleteKeyRecursive(rootKey, subKeyPath);
                    }
                }
            }
            
            // Delete the key itself
            rootKey.DeleteSubKeyTree(keyPath, false);
        }
        catch
        {
            // If key doesn't exist or can't be opened, try to delete it directly
            try
            {
                rootKey.DeleteSubKeyTree(keyPath, false);
            }
            catch
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Extracts the executable path from a full command string.
    /// Handles both quoted paths and unquoted paths with arguments.
    /// Examples:
    /// - "C:\Windows\System32\cmd.exe /k pushd "%1"" -> "C:\Windows\System32\cmd.exe"
    /// - "\"C:\Program Files\app.exe\" "%1"" -> "C:\Program Files\app.exe"
    /// </summary>
    private string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;
        
        string trimmed = command.Trim();
        
        // If the command starts with a quote, find the closing quote
        if (trimmed.StartsWith('"'))
        {
            int closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                return trimmed.Substring(1, closingQuote - 1);
            }
            // No closing quote found, return everything after the opening quote
            return trimmed.Substring(1);
        }
        
        // No leading quote - check if it's a valid file path as-is first
        if (File.Exists(trimmed))
        {
            return trimmed;
        }

        // Try to split by space, but only if the first part is an executable
        // This is heuristic: if "C:\Program Files\App.exe /arg" is passed without quotes,
        // we might split at "Program". 
        // We will try to find the longest substring that is a valid file.
        
        // Split by space
        string[] parts = trimmed.Split(' ');
        string currentPath = parts[0];
        
        // If the first part exists, return it
        if (File.Exists(currentPath)) return currentPath;

        // Otherwise, incrementally add parts and check if file exists
        for (int i = 1; i < parts.Length; i++)
        {
            currentPath += " " + parts[i];
            if (File.Exists(currentPath))
            {
                return currentPath;
            }
        }
        
        // If no valid file found, fallback to just returning the trimmed string or first part
        // We return the whole string as fallback assuming it might be a valid command (like just "cmd")
        // though we check for file existence usually.
        return trimmed;
    }
    
    private string SanitizeKeyName(string name)
    {
        // Remove invalid characters
        char[] invalidChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        string sanitized = name;
        
        foreach (char c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }
        
        // Remove leading/trailing spaces and dots
        sanitized = sanitized.Trim('.', ' ');
        
        // Ensure it's not empty
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "ContextMenuEntry";
        }
        
        return sanitized;
    }
}

public class ContextMenuEntry
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty;
    public string RegistryPath { get; set; } = string.Empty;
}

