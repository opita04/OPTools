using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OPTools.Core
{
    /// <summary>
    /// SQLite database for storing package information
    /// </summary>
    public class PackageDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private bool _disposed;

        public PackageDatabase()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OPTools"
            );
            Directory.CreateDirectory(appDataPath);
            
            var dbPath = Path.Combine(appDataPath, "npm_packages.db");
            _connection = new SqliteConnection($"Data Source={dbPath}");
            _connection.Open();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS projects (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    path TEXT NOT NULL UNIQUE,
                    last_scanned TEXT,
                    package_count INTEGER DEFAULT 0,
                    ecosystem TEXT DEFAULT 'NPM',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS packages (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    version TEXT NOT NULL,
                    path TEXT NOT NULL,
                    project_path TEXT NOT NULL,
                    is_outdated INTEGER DEFAULT 0,
                    latest_version TEXT,
                    last_checked TEXT,
                    not_found INTEGER DEFAULT 0,
                    description TEXT,
                    author TEXT,
                    license TEXT,
                    homepage TEXT,
                    repository TEXT,
                    keywords TEXT,
                    size TEXT,
                    install_date TEXT,
                    last_published TEXT,
                    maintainers TEXT,
                    engines TEXT,
                    main TEXT,
                    types TEXT,
                    dependencies_count INTEGER,
                    dev_dependencies_count INTEGER,
                    is_dev INTEGER DEFAULT 0,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL,
                    UNIQUE(name, project_path)
                );

                CREATE INDEX IF NOT EXISTS idx_packages_project ON packages(project_path);
                CREATE INDEX IF NOT EXISTS idx_packages_name ON packages(name);
            ";
            cmd.ExecuteNonQuery();
            
            // Migration for existing tables (simplistic check)
            try
            {
                using var checkCmd = _connection.CreateCommand();
                checkCmd.CommandText = "SELECT repository FROM packages LIMIT 1";
                checkCmd.ExecuteNonQuery();
            }
            catch
            {
                // Columns don't exist, add them
                var alterCommands = new[]
                {
                    "ALTER TABLE packages ADD COLUMN repository TEXT",
                    "ALTER TABLE packages ADD COLUMN keywords TEXT",
                    "ALTER TABLE packages ADD COLUMN size TEXT",
                    "ALTER TABLE packages ADD COLUMN install_date TEXT",
                    "ALTER TABLE packages ADD COLUMN last_published TEXT",
                    "ALTER TABLE packages ADD COLUMN maintainers TEXT",
                    "ALTER TABLE packages ADD COLUMN engines TEXT",
                    "ALTER TABLE packages ADD COLUMN main TEXT",
                    "ALTER TABLE packages ADD COLUMN types TEXT",
                    "ALTER TABLE packages ADD COLUMN dependencies_count INTEGER",
                    "ALTER TABLE packages ADD COLUMN dev_dependencies_count INTEGER"
                };
                
                foreach (var sql in alterCommands)
                {
                    try
                    {
                        using var alterCmd = _connection.CreateCommand();
                        alterCmd.CommandText = sql;
                        alterCmd.ExecuteNonQuery();
                    }
                    catch { /* Ignore if already exists or fails */ }
                }
            }
            
            // Migration: Add ecosystem column to projects table
            try
            {
                using var addEcosystemCmd = _connection.CreateCommand();
                addEcosystemCmd.CommandText = "ALTER TABLE projects ADD COLUMN ecosystem TEXT DEFAULT 'NPM'";
                addEcosystemCmd.ExecuteNonQuery();
            }
            catch { /* Column already exists */ }
        }

        public void UpsertProject(ProjectInfo project)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO projects (name, path, last_scanned, package_count, ecosystem, created_at, updated_at)
                VALUES (@name, @path, @lastScanned, @packageCount, @ecosystem, @createdAt, @updatedAt)
                ON CONFLICT(path) DO UPDATE SET
                    name = @name,
                    last_scanned = @lastScanned,
                    package_count = @packageCount,
                    ecosystem = @ecosystem,
                    updated_at = @updatedAt
            ";
            cmd.Parameters.AddWithValue("@name", project.Name);
            cmd.Parameters.AddWithValue("@path", project.Path);
            cmd.Parameters.AddWithValue("@lastScanned", project.LastScanned?.ToString("o") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@packageCount", project.PackageCount);
            cmd.Parameters.AddWithValue("@ecosystem", project.Ecosystem.ToString());
            cmd.Parameters.AddWithValue("@createdAt", project.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public void UpsertPackage(PackageInfo package)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO packages (name, version, path, project_path, is_outdated, latest_version, 
                    last_checked, not_found, description, author, license, homepage, repository, keywords,
                    size, install_date, last_published, maintainers, engines, main, types, 
                    dependencies_count, dev_dependencies_count, is_dev, created_at, updated_at)
                VALUES (@name, @version, @path, @projectPath, @isOutdated, @latestVersion,
                    @lastChecked, @notFound, @description, @author, @license, @homepage, @repository, @keywords,
                    @size, @installDate, @lastPublished, @maintainers, @engines, @main, @types,
                    @dependenciesCount, @devDependenciesCount, @isDev, @createdAt, @updatedAt)
                ON CONFLICT(name, project_path) DO UPDATE SET
                    version = @version,
                    path = @path,
                    is_outdated = @isOutdated,
                    latest_version = COALESCE(@latestVersion, latest_version),
                    last_checked = COALESCE(@lastChecked, last_checked),
                    not_found = @notFound,
                    description = COALESCE(@description, description),
                    author = COALESCE(@author, author),
                    license = COALESCE(@license, license),
                    homepage = COALESCE(@homepage, homepage),
                    repository = COALESCE(@repository, repository),
                    keywords = COALESCE(@keywords, keywords),
                    size = COALESCE(@size, size),
                    install_date = COALESCE(@installDate, install_date),
                    last_published = COALESCE(@lastPublished, last_published),
                    maintainers = COALESCE(@maintainers, maintainers),
                    engines = COALESCE(@engines, engines),
                    main = COALESCE(@main, main),
                    types = COALESCE(@types, types),
                    dependencies_count = COALESCE(@dependenciesCount, dependencies_count),
                    dev_dependencies_count = COALESCE(@devDependenciesCount, dev_dependencies_count),
                    is_dev = @isDev,
                    updated_at = @updatedAt
            ";
            
            AddPackageParameters(cmd, package);
            cmd.ExecuteNonQuery();
        }

        public List<PackageInfo> GetAllPackages()
        {
            var packages = new List<PackageInfo>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM packages ORDER BY name";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                packages.Add(ReadPackage(reader));
            }
            return packages;
        }

        public List<PackageInfo> GetOutdatedPackages()
        {
            var packages = new List<PackageInfo>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM packages WHERE is_outdated = 1 ORDER BY name";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                packages.Add(ReadPackage(reader));
            }
            return packages;
        }

        public List<ProjectInfo> GetAllProjects()
        {
            var projects = new List<ProjectInfo>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM projects ORDER BY name";
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var ecosystemStr = "NPM";
                try
                {
                    var ordinal = reader.GetOrdinal("ecosystem");
                    if (!reader.IsDBNull(ordinal))
                        ecosystemStr = reader.GetString(ordinal);
                }
                catch { /* Column doesn't exist yet */ }
                
                Ecosystem ecosystem = ecosystemStr switch
                {
                    "Python" => Ecosystem.Python,
                    "Cpp" => Ecosystem.Cpp,
                    "Bun" => Ecosystem.Bun,
                    _ => Ecosystem.NPM
                };
                
                projects.Add(new ProjectInfo
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Path = reader.GetString(2),
                    LastScanned = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                    PackageCount = reader.GetInt32(4),
                    Ecosystem = ecosystem,
                    CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
                    UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at")))
                });
            }
            return projects;
        }

        public List<PackageInfo> GetPackagesByProject(string projectPath)
        {
            var packages = new List<PackageInfo>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM packages WHERE project_path = @projectPath ORDER BY name";
            cmd.Parameters.AddWithValue("@projectPath", projectPath);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                packages.Add(ReadPackage(reader));
            }
            return packages;
        }

        public void UpdatePackageVersionInfo(string projectPath, string packageName, string? latestVersion, bool isOutdated, bool notFound)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE packages SET
                    latest_version = @latestVersion,
                    is_outdated = @isOutdated,
                    not_found = @notFound,
                    last_checked = @lastChecked,
                    updated_at = @updatedAt
                WHERE project_path = @projectPath AND name = @name
            ";
            cmd.Parameters.AddWithValue("@latestVersion", latestVersion ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@isOutdated", isOutdated ? 1 : 0);
            cmd.Parameters.AddWithValue("@notFound", notFound ? 1 : 0);
            cmd.Parameters.AddWithValue("@lastChecked", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@projectPath", projectPath);
            cmd.Parameters.AddWithValue("@name", packageName);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Updates the database after a package has been successfully updated via npm.
        /// Sets the version to the new version and clears the outdated flag.
        /// </summary>
        public void MarkPackageAsUpdated(string projectPath, string packageName, string newVersion)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE packages SET
                    version = @newVersion,
                    latest_version = @newVersion,
                    is_outdated = 0,
                    last_checked = @lastChecked,
                    updated_at = @updatedAt
                WHERE project_path = @projectPath AND name = @name
            ";
            cmd.Parameters.AddWithValue("@newVersion", newVersion);
            cmd.Parameters.AddWithValue("@lastChecked", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("o"));
            cmd.Parameters.AddWithValue("@projectPath", projectPath);
            cmd.Parameters.AddWithValue("@name", packageName);
            cmd.ExecuteNonQuery();
        }

        public void DeletePackage(string projectPath, string packageName)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM packages WHERE project_path = @projectPath AND name = @name";
            cmd.Parameters.AddWithValue("@projectPath", projectPath);
            cmd.Parameters.AddWithValue("@name", packageName);
            cmd.ExecuteNonQuery();
            
            UpdateProjectPackageCount(projectPath);
        }

        public void DeleteProject(string projectPath)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM packages WHERE project_path = @projectPath";
                cmd.Parameters.AddWithValue("@projectPath", projectPath);
                cmd.ExecuteNonQuery();
                
                using var cmd2 = _connection.CreateCommand();
                cmd2.Transaction = transaction;
                cmd2.CommandText = "DELETE FROM projects WHERE path = @projectPath";
                cmd2.Parameters.AddWithValue("@projectPath", projectPath);
                cmd2.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Clears all data from the database (projects and packages)
        /// </summary>
        public void ClearAllData()
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM packages";
            cmd.ExecuteNonQuery();
            
            using var cmd2 = _connection.CreateCommand();
            cmd2.CommandText = "DELETE FROM projects";
            cmd2.ExecuteNonQuery();
        }

        private void UpdateProjectPackageCount(string projectPath)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE projects SET package_count = (
                    SELECT COUNT(*) FROM packages WHERE project_path = @projectPath
                ) WHERE path = @projectPath
            ";
            cmd.Parameters.AddWithValue("@projectPath", projectPath);
            cmd.ExecuteNonQuery();
        }

        private PackageInfo ReadPackage(SqliteDataReader reader)
        {
            return new PackageInfo
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                Version = reader.GetString(reader.GetOrdinal("version")),
                Path = reader.GetString(reader.GetOrdinal("path")),
                ProjectPath = reader.GetString(reader.GetOrdinal("project_path")),
                IsOutdated = reader.GetInt32(reader.GetOrdinal("is_outdated")) == 1,
                LatestVersion = IsDbNull(reader, "latest_version") ? null : reader.GetString(reader.GetOrdinal("latest_version")),
                LastChecked = IsDbNull(reader, "last_checked") ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_checked"))),
                NotFound = reader.GetInt32(reader.GetOrdinal("not_found")) == 1,
                Description = IsDbNull(reader, "description") ? null : reader.GetString(reader.GetOrdinal("description")),
                Author = IsDbNull(reader, "author") ? null : reader.GetString(reader.GetOrdinal("author")),
                License = IsDbNull(reader, "license") ? null : reader.GetString(reader.GetOrdinal("license")),
                Homepage = IsDbNull(reader, "homepage") ? null : reader.GetString(reader.GetOrdinal("homepage")),
                Repository = IsDbNull(reader, "repository") ? null : reader.GetString(reader.GetOrdinal("repository")),
                Keywords = IsDbNull(reader, "keywords") ? null : reader.GetString(reader.GetOrdinal("keywords")).Split(',').ToList(),
                Size = IsDbNull(reader, "size") ? null : reader.GetString(reader.GetOrdinal("size")),
                InstallDate = IsDbNull(reader, "install_date") ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("install_date"))),
                LastPublished = IsDbNull(reader, "last_published") ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_published"))),
                Maintainers = IsDbNull(reader, "maintainers") ? null : reader.GetString(reader.GetOrdinal("maintainers")).Split(',').ToList(),
                Engines = IsDbNull(reader, "engines") ? null : reader.GetString(reader.GetOrdinal("engines")),
                Main = IsDbNull(reader, "main") ? null : reader.GetString(reader.GetOrdinal("main")),
                Types = IsDbNull(reader, "types") ? null : reader.GetString(reader.GetOrdinal("types")),
                DependenciesCount = IsDbNull(reader, "dependencies_count") ? null : reader.GetInt32(reader.GetOrdinal("dependencies_count")),
                DevDependenciesCount = IsDbNull(reader, "dev_dependencies_count") ? null : reader.GetInt32(reader.GetOrdinal("dev_dependencies_count")),
                IsDev = reader.GetInt32(reader.GetOrdinal("is_dev")) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
                UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at")))
            };
        }

        private void AddPackageParameters(SqliteCommand cmd, PackageInfo package)
        {
            // Helper function to join lists or null
            string? JoinList(List<string>? list) => list != null && list.Count > 0 ? string.Join(",", list) : null;

            cmd.Parameters.AddWithValue("@name", package.Name);
            cmd.Parameters.AddWithValue("@version", package.Version);
            cmd.Parameters.AddWithValue("@path", package.Path);
            cmd.Parameters.AddWithValue("@projectPath", package.ProjectPath);
            cmd.Parameters.AddWithValue("@isOutdated", package.IsOutdated ? 1 : 0);
            cmd.Parameters.AddWithValue("@latestVersion", package.LatestVersion ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lastChecked", package.LastChecked?.ToString("o") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@notFound", package.NotFound ? 1 : 0);
            cmd.Parameters.AddWithValue("@description", package.Description ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@author", package.Author ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@license", package.License ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@homepage", package.Homepage ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@repository", package.Repository ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@keywords", JoinList(package.Keywords) ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@size", package.Size ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@installDate", package.InstallDate?.ToString("o") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lastPublished", package.LastPublished?.ToString("o") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@maintainers", JoinList(package.Maintainers) ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@engines", package.Engines ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@main", package.Main ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@types", package.Types ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@dependenciesCount", package.DependenciesCount ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@devDependenciesCount", package.DevDependenciesCount ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@isDev", package.IsDev ? 1 : 0);
            cmd.Parameters.AddWithValue("@createdAt", package.CreatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("o"));
        }

        private bool IsDbNull(SqliteDataReader reader, string columnName)
        {
            try
            {
                return reader.IsDBNull(reader.GetOrdinal(columnName));
            }
            catch
            {
                // Column might not exist yet if migration failed or fresh db mismatch (shouldn't happen with migration logic)
                return true;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Close();
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}
