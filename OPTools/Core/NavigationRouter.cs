using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OPTools.Core;

/// <summary>
/// Handles navigation between different panels/views in the main form.
/// Centralizes navigation logic and panel visibility management.
/// </summary>
public class NavigationRouter
{
    private readonly Panel _contentPanel;
    private readonly Dictionary<string, Panel> _panels;
    private readonly Dictionary<string, Action> _initializers;
    
    /// <summary>
    /// Event raised when navigation occurs. Provides the current view name.
    /// </summary>
    public event Action<string>? Navigated;
    
    public NavigationRouter(Panel contentPanel)
    {
        _contentPanel = contentPanel ?? throw new ArgumentNullException(nameof(contentPanel));
        _panels = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        _initializers = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Registers a panel for a specific view name.
    /// </summary>
    /// <param name="viewName">The unique identifier for this view.</param>
    /// <param name="panel">The panel to display when this view is active.</param>
    /// <param name="initializer">Optional action to run when this view is activated (e.g., refresh data).</param>
    public void RegisterPanel(string viewName, Panel panel, Action? initializer = null)
    {
        if (string.IsNullOrWhiteSpace(viewName))
            throw new ArgumentException("View name cannot be null or whitespace.", nameof(viewName));
        
        if (panel == null)
            throw new ArgumentNullException(nameof(panel));
        
        _panels[viewName] = panel;
        
        if (initializer != null)
        {
            _initializers[viewName] = initializer;
        }
    }
    
    /// <summary>
    /// Navigates to the specified view.
    /// </summary>
    /// <param name="viewName">The view name to navigate to.</param>
    public void NavigateTo(string viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName))
            throw new ArgumentException("View name cannot be null or whitespace.", nameof(viewName));
        
        // Hide all panels
        foreach (var kvp in _panels)
        {
            kvp.Value.Visible = false;
        }
        
        // Show the target panel if registered
        if (_panels.TryGetValue(viewName, out Panel? panel))
        {
            panel.Visible = true;
            
            // Run initializer if exists
            if (_initializers.TryGetValue(viewName, out Action? initializer))
            {
                initializer();
            }
        }
        
        // Raise navigation event
        Navigated?.Invoke(viewName);
    }
    
    /// <summary>
    /// Gets the currently registered view names.
    /// </summary>
    public IEnumerable<string> GetRegisteredViews()
    {
        return _panels.Keys.OrderBy(k => k);
    }
    
    /// <summary>
    /// Checks if a view is registered.
    /// </summary>
    public bool IsViewRegistered(string viewName)
    {
        return !string.IsNullOrWhiteSpace(viewName) && _panels.ContainsKey(viewName);
    }
}
