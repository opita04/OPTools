using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OPTools.Core
{
    /// <summary>
    /// Scans directories for packages across multiple ecosystems (NPM, Python, C++)
    /// </summary>
    public class NpmScanner
    {
        private readonly HashSet<string> _excludedDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules",
            ".git",
            ".svn",
            "bin",
            "obj",
            "dist",
            "build",
            ".next",
            ".nuxt",
            "coverage",
            ".cache",
            "__pycache__",
            ".venv",
            "venv",
            "env",
            ".tox"
        };

        // Python project markers (order matters - first found wins)
        private readonly string[] _pythonMarkers = 
        {
            "pyproject.toml",
            "setup.py",
            "requirements.txt",
            "Pipfile",
            "setup.cfg"
        };

        // C++ project markers
        private readonly string[] _cppMarkers = 
        {
            "CMakeLists.txt",
            "vcpkg.json",
            "conanfile.txt",
            "conanfile.py",
            "Makefile"
        };

        /// <summary>
        /// Scans a directory for projects of the specified ecosystem type.
        /// </summary>
        public async Task<NpmScanResult> ScanDirectoryAsync(string rootPath, Ecosystem ecosystem, IProgress<string>? progress = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new NpmScanResult();
            
            await Task.Run(() =>
            {
                progress?.Report($"Scanning for {ecosystem} projects in {Path.GetFileName(rootPath)}...");
                
                switch (ecosystem)
                {
                    case Ecosystem.NPM:
                        ScanForNpmProjects(rootPath, result, progress);
                        break;
                    case Ecosystem.Python:
                        ScanForPythonProjects(rootPath, result, progress);
                        break;
                    case Ecosystem.Cpp:
                        ScanForCppProjects(rootPath, result, progress);
                        break;
                }
            });
            
            stopwatch.Stop();
            result.ScanDuration = stopwatch.Elapsed;
            result.PackagesFound = result.Packages.Count;
            result.NewPackages = result.Packages.Count;
            
            return result;
        }

        /// <summary>
        /// Scans a directory for npm projects (legacy overload for backward compatibility).
        /// </summary>
        public async Task<NpmScanResult> ScanDirectoryAsync(string rootPath, IProgress<string>? progress = null)
        {
            return await ScanDirectoryAsync(rootPath, Ecosystem.NPM, progress);
        }

        /// <summary>
        /// Scans a specific folder as a project, skipping recursive discovery.
        /// </summary>
        public async Task<NpmScanResult> ScanSingleProjectAsync(string projectPath, Ecosystem? ecosystem, IProgress<string>? progress = null)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new NpmScanResult();
            
            await Task.Run(() =>
            {
                progress?.Report($"Scanning single project: {Path.GetFileName(projectPath)}");
                
                // Determine which ecosystems to check
                var ecosystemsToCheck = ecosystem.HasValue 
                    ? new[] { ecosystem.Value }
                    : new[] { Ecosystem.NPM, Ecosystem.Python, Ecosystem.Cpp };

                bool foundInfo = false;

                foreach (var eco in ecosystemsToCheck)
                {
                    switch (eco)
                    {
                        case Ecosystem.NPM:
                            var packageJsonPath = Path.Combine(projectPath, "package.json");
                            if (File.Exists(packageJsonPath))
                            {
                                ProcessNpmProjectAtPath(packageJsonPath, projectPath, result, progress);
                                foundInfo = true;
                            }
                            break;
                            
                        case Ecosystem.Python:
                            var pyMarker = FindPythonMarker(projectPath);
                            if (pyMarker != null)
                            {
                                ProcessPythonProjectAtPath(projectPath, pyMarker, result, progress);
                                foundInfo = true;
                            }
                            break;
                            
                        case Ecosystem.Cpp:
                            var cppMarker = FindCppMarker(projectPath);
                            if (cppMarker != null)
                            {
                                ProcessCppProjectAtPath(projectPath, cppMarker, result, progress);
                                foundInfo = true;
                            }
                            break;
                    }
                }

                // If no packages found but user forced it, maybe create an empty project entry?
                // For now, if no markers found, we might warn or just return empty result.
                // But the user said "create package for that based on the name of the folder".
                // If we didn't find any packages/markers, let's at least create a project entry if it doesn't exist.
                if (!foundInfo && result.Projects.Count == 0)
                {
                    // Fallback: Create generic project entry even if empty
                     var project = new NpmProject
                    {
                        Name = Path.GetFileName(projectPath),
                        Path = projectPath,
                        Ecosystem = ecosystem ?? Ecosystem.NPM, // Default to NPM if unknown
                        LastScanned = DateTime.Now,
                        PackageCount = 0,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    result.Projects.Add(project);
                }
            });
            
            stopwatch.Stop();
            result.ScanDuration = stopwatch.Elapsed;
            result.PackagesFound = result.Packages.Count;
            result.NewPackages = result.Packages.Count;
            
            return result;
        }

        #region NPM Scanning

        private void ScanForNpmProjects(string rootPath, NpmScanResult result, IProgress<string>? progress)
        {
            // First, check if the selected directory itself is a project
            var rootPackageJsonPath = Path.Combine(rootPath, "package.json");
            
            if (File.Exists(rootPackageJsonPath) && IsValidProjectPackageJson(rootPackageJsonPath))
            {
                // Selected folder IS a project - scan only this one (no recursive discovery)
                progress?.Report($"Scanning project: {Path.GetFileName(rootPath)}");
                ProcessNpmProjectAtPath(rootPackageJsonPath, rootPath, result, progress);
            }
            else
            {
                // Selected folder is NOT a project - recursively find all npm projects in subdirectories
                var packageJsonFiles = FindPackageJsonFiles(rootPath, progress);
                
                if (packageJsonFiles.Count == 0)
                {
                    progress?.Report($"No NPM projects found in {Path.GetFileName(rootPath)}");
                    return;
                }
                
                progress?.Report($"Found {packageJsonFiles.Count} NPM project(s). Parsing...");
                
                foreach (var packageJsonPath in packageJsonFiles)
                {
                    var projectPath = Path.GetDirectoryName(packageJsonPath);
                    if (projectPath != null)
                    {
                        ProcessNpmProjectAtPath(packageJsonPath, projectPath, result, progress);
                    }
                }
            }
        }

        #endregion

        #region Python Scanning

        private void ScanForPythonProjects(string rootPath, NpmScanResult result, IProgress<string>? progress)
        {
            // Check if root is a Python project
            var rootMarker = FindPythonMarker(rootPath);
            if (rootMarker != null)
            {
                progress?.Report($"Scanning Python project: {Path.GetFileName(rootPath)}");
                ProcessPythonProjectAtPath(rootPath, rootMarker, result, progress);
            }
            else
            {
                // Recursively find Python projects
                var pythonProjects = FindPythonProjects(rootPath, progress);
                
                if (pythonProjects.Count == 0)
                {
                    progress?.Report($"No Python projects found in {Path.GetFileName(rootPath)}");
                    return;
                }
                
                progress?.Report($"Found {pythonProjects.Count} Python project(s). Parsing...");
                
                foreach (var (projectPath, marker) in pythonProjects)
                {
                    ProcessPythonProjectAtPath(projectPath, marker, result, progress);
                }
            }
        }

        private string? FindPythonMarker(string directory)
        {
            foreach (var marker in _pythonMarkers)
            {
                if (File.Exists(Path.Combine(directory, marker)))
                    return marker;
            }
            return null;
        }

        private List<(string Path, string Marker)> FindPythonProjects(string rootPath, IProgress<string>? progress)
        {
            var projects = new List<(string, string)>();
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                var currentDir = queue.Dequeue();
                try
                {
                    var dirName = Path.GetFileName(currentDir);
                    if (_excludedDirs.Contains(dirName)) continue;

                    var marker = FindPythonMarker(currentDir);
                    if (marker != null)
                    {
                        projects.Add((currentDir, marker));
                        progress?.Report($"Found: {dirName}");
                        continue; // Don't recurse into project directories
                    }

                    foreach (var subDir in Directory.GetDirectories(currentDir))
                    {
                        var subDirName = Path.GetFileName(subDir);
                        if (!_excludedDirs.Contains(subDirName))
                            queue.Enqueue(subDir);
                    }
                }
                catch (UnauthorizedAccessException uex) { System.Diagnostics.Debug.WriteLine($"Access denied scanning {currentDir}: {uex.Message}"); }
                catch (DirectoryNotFoundException dex) { System.Diagnostics.Debug.WriteLine($"Directory not found {currentDir}: {dex.Message}"); }
            }
            return projects;
        }

        private void ProcessPythonProjectAtPath(string projectPath, string markerFile, NpmScanResult result, IProgress<string>? progress)
        {
            try
            {
                var projectName = Path.GetFileName(projectPath);
                progress?.Report($"Parsing Python: {projectName}");

                var packages = ParsePythonDependencies(projectPath, markerFile);

                var project = new NpmProject
                {
                    Name = projectName,
                    Path = projectPath,
                    Ecosystem = Ecosystem.Python,
                    LastScanned = DateTime.Now,
                    PackageCount = packages.Count,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                result.Projects.Add(project);
                result.Packages.AddRange(packages);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing Python project {projectPath}: {ex.Message}");
            }
        }

        private List<NpmPackage> ParsePythonDependencies(string projectPath, string markerFile)
        {
            var packages = new List<NpmPackage>();
            var filePath = Path.Combine(projectPath, markerFile);

            try
            {
                if (markerFile == "requirements.txt")
                {
                    var lines = File.ReadAllLines(filePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("-"))
                            continue;

                        // Parse package==version or package>=version etc.
                        var parts = System.Text.RegularExpressions.Regex.Split(trimmed, @"[><=!~]+");
                        var name = parts[0].Trim();
                        var version = parts.Length > 1 ? parts[1].Trim() : "*";

                        if (!string.IsNullOrEmpty(name))
                        {
                            packages.Add(new NpmPackage
                            {
                                Name = name,
                                Version = version,
                                Path = projectPath,
                                ProjectPath = projectPath,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            });
                        }
                    }
                }
                else if (markerFile == "pyproject.toml")
                {
                    // Basic TOML parsing for dependencies
                    var content = File.ReadAllText(filePath);
                    var inDependencies = false;
                    foreach (var line in content.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("[project.dependencies]") || trimmed.StartsWith("[tool.poetry.dependencies]"))
                        {
                            inDependencies = true;
                            continue;
                        }
                        if (trimmed.StartsWith("[") && inDependencies)
                        {
                            inDependencies = false;
                        }
                        if (inDependencies && trimmed.Contains("="))
                        {
                            var parts = trimmed.Split('=');
                            var name = parts[0].Trim().Trim('"');
                            var version = parts.Length > 1 ? parts[1].Trim().Trim('"', ',') : "*";
                            if (!string.IsNullOrEmpty(name) && name != "python")
                            {
                                packages.Add(new NpmPackage
                                {
                                    Name = name,
                                    Version = version,
                                    Path = projectPath,
                                    ProjectPath = projectPath,
                                    CreatedAt = DateTime.Now,
                                    UpdatedAt = DateTime.Now
                                });
                            }
                        }
                    }
                }
                // Add Pipfile parsing if needed - similar pattern
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing Python dependencies: {ex.Message}");
            }

            return packages;
        }

        #endregion

        #region C++ Scanning

        private void ScanForCppProjects(string rootPath, NpmScanResult result, IProgress<string>? progress)
        {
            // Check if root is a C++ project
            var rootMarker = FindCppMarker(rootPath);
            if (rootMarker != null)
            {
                progress?.Report($"Scanning C++ project: {Path.GetFileName(rootPath)}");
                ProcessCppProjectAtPath(rootPath, rootMarker, result, progress);
            }
            else
            {
                // Recursively find C++ projects
                var cppProjects = FindCppProjects(rootPath, progress);
                
                if (cppProjects.Count == 0)
                {
                    progress?.Report($"No C++ projects found in {Path.GetFileName(rootPath)}");
                    return;
                }
                
                progress?.Report($"Found {cppProjects.Count} C++ project(s). Parsing...");
                
                foreach (var (projectPath, marker) in cppProjects)
                {
                    ProcessCppProjectAtPath(projectPath, marker, result, progress);
                }
            }
        }

        private string? FindCppMarker(string directory)
        {
            // Check for .vcxproj files
            try
            {
                var vcxprojFiles = Directory.GetFiles(directory, "*.vcxproj");
                if (vcxprojFiles.Length > 0)
                    return Path.GetFileName(vcxprojFiles[0]);
            }
            catch { }

            foreach (var marker in _cppMarkers)
            {
                if (File.Exists(Path.Combine(directory, marker)))
                    return marker;
            }
            return null;
        }

        private List<(string Path, string Marker)> FindCppProjects(string rootPath, IProgress<string>? progress)
        {
            var projects = new List<(string, string)>();
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                var currentDir = queue.Dequeue();
                try
                {
                    var dirName = Path.GetFileName(currentDir);
                    if (_excludedDirs.Contains(dirName)) continue;

                    var marker = FindCppMarker(currentDir);
                    if (marker != null)
                    {
                        projects.Add((currentDir, marker));
                        progress?.Report($"Found: {dirName}");
                        continue; // Don't recurse into project directories
                    }

                    foreach (var subDir in Directory.GetDirectories(currentDir))
                    {
                        var subDirName = Path.GetFileName(subDir);
                        if (!_excludedDirs.Contains(subDirName))
                            queue.Enqueue(subDir);
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (DirectoryNotFoundException) { }
            }
            return projects;
        }

        private void ProcessCppProjectAtPath(string projectPath, string markerFile, NpmScanResult result, IProgress<string>? progress)
        {
            try
            {
                var projectName = Path.GetFileName(projectPath);
                progress?.Report($"Parsing C++: {projectName}");

                var packages = ParseCppDependencies(projectPath, markerFile);

                var project = new NpmProject
                {
                    Name = projectName,
                    Path = projectPath,
                    Ecosystem = Ecosystem.Cpp,
                    LastScanned = DateTime.Now,
                    PackageCount = packages.Count,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                result.Projects.Add(project);
                result.Packages.AddRange(packages);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing C++ project {projectPath}: {ex.Message}");
            }
        }

        private List<NpmPackage> ParseCppDependencies(string projectPath, string markerFile)
        {
            var packages = new List<NpmPackage>();
            var filePath = Path.Combine(projectPath, markerFile);

            try
            {
                if (markerFile == "vcpkg.json")
                {
                    var content = File.ReadAllText(filePath);
                    var json = JObject.Parse(content);
                    var dependencies = json["dependencies"] as JArray;
                    if (dependencies != null)
                    {
                        foreach (var dep in dependencies)
                        {
                            var name = dep.Type == JTokenType.String ? dep.ToString() : dep["name"]?.ToString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                packages.Add(new NpmPackage
                                {
                                    Name = name,
                                    Version = "*",
                                    Path = projectPath,
                                    ProjectPath = projectPath,
                                    CreatedAt = DateTime.Now,
                                    UpdatedAt = DateTime.Now
                                });
                            }
                        }
                    }
                }
                else if (markerFile == "conanfile.txt")
                {
                    var lines = File.ReadAllLines(filePath);
                    var inRequires = false;
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed == "[requires]")
                        {
                            inRequires = true;
                            continue;
                        }
                        if (trimmed.StartsWith("[") && inRequires)
                        {
                            inRequires = false;
                        }
                        if (inRequires && !string.IsNullOrEmpty(trimmed))
                        {
                            var parts = trimmed.Split('/');
                            var name = parts[0];
                            var version = parts.Length > 1 ? parts[1].Split('@')[0] : "*";
                            packages.Add(new NpmPackage
                            {
                                Name = name,
                                Version = version,
                                Path = projectPath,
                                ProjectPath = projectPath,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            });
                        }
                    }
                }
                // CMakeLists.txt parsing would require more complex analysis
                // For now, we just mark it as a C++ project without parsing deps
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing C++ dependencies: {ex.Message}");
            }

            return packages;
        }

        #endregion

        /// <summary>
        /// Processes a single NPM project at the given path and adds it to the result.
        /// </summary>
        private void ProcessNpmProjectAtPath(string packageJsonPath, string projectPath, NpmScanResult result, IProgress<string>? progress)
        {
            try
            {
                var projectName = Path.GetFileName(projectPath);
                progress?.Report($"Parsing: {projectName}");
                
                var packages = ParsePackageJson(packageJsonPath, projectPath);
                
                if (packages.Count > 0)
                {
                    var project = new NpmProject
                    {
                        Name = projectName,
                        Path = projectPath,
                        LastScanned = DateTime.Now,
                        PackageCount = packages.Count,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    
                    result.Projects.Add(project);
                    result.Packages.AddRange(packages);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing {packageJsonPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Scans globally installed npm packages
        /// </summary>
        public async Task<List<NpmPackage>> ScanGlobalPackagesAsync(IProgress<string>? progress = null)
        {
            var packages = new List<NpmPackage>();
            
            try
            {
                progress?.Report("Scanning global npm packages...");
                
                var result = await RunNpmCommandAsync("npm list -g --depth=0 --json");
                
                if (!string.IsNullOrEmpty(result))
                {
                    var json = JObject.Parse(result);
                    var dependencies = json["dependencies"] as JObject;
                    
                    if (dependencies != null)
                    {
                        foreach (var prop in dependencies.Properties())
                        {
                            var packageName = prop.Name;
                            var packageInfo = prop.Value as JObject;
                            var version = packageInfo?["version"]?.ToString() ?? "unknown";
                            
                            packages.Add(new NpmPackage
                            {
                                Name = packageName,
                                Version = version,
                                Path = "__GLOBAL__",
                                ProjectPath = "__GLOBAL__",
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error scanning global packages: {ex.Message}");
            }
            
            return packages;
        }

        private List<string> FindPackageJsonFiles(string rootPath, IProgress<string>? progress)
        {
            var files = new List<string>();
            
            try
            {
                var queue = new Queue<string>();
                queue.Enqueue(rootPath);
                
                while (queue.Count > 0)
                {
                    var currentDir = queue.Dequeue();
                    
                    try
                    {
                        var dirName = Path.GetFileName(currentDir);
                        
                        // Skip excluded directories
                        if (_excludedDirs.Contains(dirName))
                            continue;
                        
                        // Check for package.json in this directory
                        var packageJsonPath = Path.Combine(currentDir, "package.json");
                        if (File.Exists(packageJsonPath))
                        {
                            // Validate this is a real project (has name or dependencies)
                            if (IsValidProjectPackageJson(packageJsonPath))
                            {
                                files.Add(packageJsonPath);
                                progress?.Report($"Found: {dirName}");
                                
                                // DON'T recurse into this directory - it's a project root
                                // This prevents nested package.json files (e.g., in packages/, libs/, etc.) 
                                // from being treated as separate projects
                                continue;
                            }
                        }
                        
                        // Add subdirectories to queue (only if we didn't find a valid project above)
                        foreach (var subDir in Directory.GetDirectories(currentDir))
                        {
                            var subDirName = Path.GetFileName(subDir);
                            if (!_excludedDirs.Contains(subDirName))
                            {
                                queue.Enqueue(subDir);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible directories
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Skip deleted directories
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error finding package.json files: {ex.Message}");
            }
            
            return files;
        }

        /// <summary>
        /// Validates if a package.json represents a real project (not a config file or partial)
        /// </summary>
        private bool IsValidProjectPackageJson(string packageJsonPath)
        {
            try
            {
                var content = File.ReadAllText(packageJsonPath);
                var json = JObject.Parse(content);
                
                // Must have a name to be considered a real project
                var name = json["name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    return false;
                
                // Must have dependencies or devDependencies
                var hasDeps = json["dependencies"] != null || json["devDependencies"] != null;
                
                return hasDeps;
            }
            catch
            {
                return false;
            }
        }

        private List<NpmPackage> ParsePackageJson(string packageJsonPath, string projectPath)
        {
            var packages = new List<NpmPackage>();
            
            try
            {
                var content = File.ReadAllText(packageJsonPath);
                var json = JObject.Parse(content);
                
                // Parse dependencies
                var dependencies = json["dependencies"] as JObject;
                if (dependencies != null)
                {
                    foreach (var prop in dependencies.Properties())
                    {
                        packages.Add(CreatePackageFromDependency(prop.Name, prop.Value?.ToString() ?? "*", projectPath, false));
                    }
                }
                
                // Parse devDependencies
                var devDependencies = json["devDependencies"] as JObject;
                if (devDependencies != null)
                {
                    foreach (var prop in devDependencies.Properties())
                    {
                        packages.Add(CreatePackageFromDependency(prop.Name, prop.Value?.ToString() ?? "*", projectPath, true));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing package.json: {ex.Message}");
            }
            
            return packages;
        }

        private NpmPackage CreatePackageFromDependency(string name, string versionSpec, string projectPath, bool isDev)
        {
            var version = CleanVersionSpec(versionSpec);
            var installedPath = Path.Combine(projectPath, "node_modules", name);
            var packageJsonPath = Path.Combine(installedPath, "package.json");
            
            var package = new NpmPackage
            {
                Name = name,
                Version = version, // Default to requested version until we verify installed
                Path = installedPath,
                ProjectPath = projectPath,
                IsDev = isDev,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                
                // Defaults
                NotFound = true
            };

            // Try to read actual installed version and metadata
            if (File.Exists(packageJsonPath))
            {
                try
                {
                    var content = File.ReadAllText(packageJsonPath);
                    var json = JObject.Parse(content);
                    
                    package.NotFound = false;
                    package.Version = json["version"]?.ToString() ?? version;
                    package.Description = json["description"]?.ToString();
                    package.Homepage = json["homepage"]?.ToString();
                    package.License = json["license"]?.Type == JTokenType.Object 
                        ? json["license"]["type"]?.ToString() 
                        : json["license"]?.ToString();

                    // Author handling (string or object)
                    var authorToken = json["author"];
                    if (authorToken?.Type == JTokenType.Object)
                    {
                        package.Author = authorToken["name"]?.ToString();
                    }
                    else
                    {
                        package.Author = authorToken?.ToString();
                    }

                    // Repository handling (string or object)
                    var repoToken = json["repository"];
                    if (repoToken?.Type == JTokenType.Object)
                    {
                        package.Repository = repoToken["url"]?.ToString();
                        // Cleanup git+ prefix/suffix
                        if (package.Repository != null)
                        {
                             package.Repository = package.Repository.Replace("git+", "").Replace(".git", "");
                        }
                    }
                    else
                    {
                        package.Repository = repoToken?.ToString();
                    }

                    // Keywords
                    var keywordsToken = json["keywords"];
                    if (keywordsToken?.Type == JTokenType.Array)
                    {
                        package.Keywords = keywordsToken.Select(k => k.ToString()).ToList();
                    }

                    // Engines
                    var enginesToken = json["engines"];
                    if (enginesToken?.Type == JTokenType.Object)
                    {
                        package.Engines = string.Join(", ", enginesToken.Children<JProperty>().Select(p => $"{p.Name}: {p.Value}"));
                    }

                    // Maintainers
                    // Some packages have 'maintainers' array
                    var maintainersToken = json["maintainers"];
                    if (maintainersToken?.Type == JTokenType.Array)
                    {
                        package.Maintainers = maintainersToken.Select(m => 
                            m.Type == JTokenType.Object ? m["name"]?.ToString() ?? "" : m.ToString()
                        ).Where(s => !string.IsNullOrEmpty(s)).ToList();
                    }

                    // Size (Not easily available from package.json alone without calculating folder size, skipping for now to avoid perf hit)
                    
                    // Install Date (File creation time)
                    package.InstallDate = File.GetCreationTime(packageJsonPath);
                    
                    // Main/Types
                    package.Main = json["main"]?.ToString();
                    package.Types = json["types"]?.ToString() ?? json["typings"]?.ToString();
                    
                    // Dependencies counts
                    package.DependenciesCount = json["dependencies"]?.Count() ?? 0;
                    package.DevDependenciesCount = json["devDependencies"]?.Count() ?? 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading installed package {name}: {ex.Message}");
                }
            }
            
            return package;
        }

        private string CleanVersionSpec(string versionSpec)
        {
            if (string.IsNullOrEmpty(versionSpec))
                return "*";
            
            // Remove common version prefixes
            var cleaned = versionSpec
                .TrimStart('^', '~', '>', '<', '=', ' ')
                .Split(' ')[0];
            
            // Handle npm: or workspace: protocols
            if (cleaned.Contains(':'))
            {
                var parts = cleaned.Split(':');
                cleaned = parts.Length > 1 ? parts[1] : parts[0];
            }
            
            return string.IsNullOrEmpty(cleaned) ? versionSpec : cleaned;
        }

        private async Task<string> RunNpmCommandAsync(string command)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output;
        }
    }
}
