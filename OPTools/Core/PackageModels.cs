using System;
using System.Collections.Generic;

namespace OPTools.Core
{
    /// <summary>
    /// Represents the package ecosystem type for a project
    /// </summary>
    public enum Ecosystem
    {
        NPM,
        Python,
        Cpp,
        Bun
    }

    /// <summary>
    /// Represents an package discovered in a project
    /// </summary>
    public class PackageInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public bool IsOutdated { get; set; }
        public string? LatestVersion { get; set; }
        public DateTime? LastChecked { get; set; }
        public bool NotFound { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Enhanced fields (matching NPM Handler)
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? License { get; set; }
        public string? Homepage { get; set; }
        public string? Repository { get; set; }
        public List<string>? Keywords { get; set; }
        public string? Size { get; set; }
        public DateTime? InstallDate { get; set; }
        public DateTime? LastPublished { get; set; }
        public List<string>? Maintainers { get; set; }
        public string? Engines { get; set; }
        public string? Main { get; set; }
        public string? Types { get; set; }
        public int? DependenciesCount { get; set; }
        public int? DevDependenciesCount { get; set; }
        public bool IsDev { get; set; }
        public Ecosystem Ecosystem { get; set; } = Ecosystem.NPM;
        
        public string DisplayProjectPath => ProjectPath switch
        {
            "__GLOBAL__" => "Global NPM",
            "__GLOBAL_BUN__" => "Global Bun",
            "__GLOBAL_PYTHON__" => "Global Python",
            _ => ProjectPath
        };
        
        public string StatusText
        {
            get
            {
                if (NotFound) return "Not Found";
                if (IsOutdated) return "Outdated";
                if (LastChecked.HasValue) return "Up to date";
                return "Unknown";
            }
        }
        
        /// <summary>
        /// Gets the npm registry URL for this package
        /// </summary>
        public string NpmUrl => $"https://www.npmjs.com/package/{Name}";
    }

    /// <summary>
    /// Represents a project containing packages
    /// </summary>
    public class ProjectInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime? LastScanned { get; set; }
        public int PackageCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Ecosystem Ecosystem { get; set; } = Ecosystem.NPM;
        
        public string DisplayName => Path switch
        {
            "__GLOBAL__" => "Global NPM Packages",
            "__GLOBAL_BUN__" => "Global Bun Packages",
            "__GLOBAL_PYTHON__" => "Global Python Packages",
            _ => Name
        };
    }

    /// <summary>
    /// Results from scanning a directory for packages
    /// </summary>
    public class PackageScanResult
    {
        public int PackagesFound { get; set; }
        public int NewPackages { get; set; }
        public int UpdatedPackages { get; set; }
        public TimeSpan ScanDuration { get; set; }
        public List<PackageInfo> Packages { get; set; } = new();
        public List<ProjectInfo> Projects { get; set; } = new();
    }

    /// <summary>
    /// Result from updating a single package
    /// </summary>
    public class PackageUpdateResult
    {
        public string PackageName { get; set; } = string.Empty;
        public string OldVersion { get; set; } = string.Empty;
        public string NewVersion { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Details { get; set; }
    }

    /// <summary>
    /// Filter options for the package list
    /// </summary>
    public class PackageFilterOptions
    {
        public string SearchTerm { get; set; } = string.Empty;
        public bool ShowOutdatedOnly { get; set; }
        public string? SelectedProject { get; set; }
    }

    /// <summary>
    /// Data for export functionality
    /// </summary>
    public class PackageExportData
    {
        public List<PackageInfo> Packages { get; set; } = new();
        public List<ProjectInfo> Projects { get; set; } = new();
        public DateTime ExportDate { get; set; } = DateTime.Now;
    }
}
