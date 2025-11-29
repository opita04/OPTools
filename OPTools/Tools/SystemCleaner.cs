using System;
using System.IO;
using System.Threading.Tasks;

namespace OPTools.Tools;

public static class SystemCleaner
{
    public static async Task<CleanResult> RemovePrefetchFiles()
    {
        CleanResult result = new CleanResult();
        string prefetchPath = Path.Combine(Environment.SystemDirectory, "..", "Prefetch");

        await Task.Run(() =>
        {
            try
            {
                prefetchPath = Path.GetFullPath(prefetchPath);
                
                if (Directory.Exists(prefetchPath))
                {
                    DirectoryInfo dir = new DirectoryInfo(prefetchPath);
                    foreach (FileInfo file in dir.GetFiles())
                    {
                        try
                        {
                            file.Delete();
                            result.FilesDeleted++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Failed to delete {file.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error removing prefetch files: {ex.Message}");
            }
        });

        result.Success = result.Errors.Count == 0;
        return result;
    }

    public static async Task<CleanResult> EmptyRecycleBin()
    {
        CleanResult result = new CleanResult();

        await Task.Run(() =>
        {
            try
            {
                string[] drives = { "C", "D" };
                
                foreach (string drive in drives)
                {
                    string recycleBinPath = $"{drive}:\\$Recycle.Bin";
                    
                    if (Directory.Exists(recycleBinPath))
                    {
                        try
                        {
                            DirectoryInfo dir = new DirectoryInfo(recycleBinPath);
                            foreach (DirectoryInfo subDir in dir.GetDirectories())
                            {
                                try
                                {
                                    subDir.Delete(true);
                                    result.FoldersDeleted++;
                                }
                                catch { }
                            }
                            
                            foreach (FileInfo file in dir.GetFiles("*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    file.Delete();
                                    result.FilesDeleted++;
                                }
                                catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Error cleaning recycle bin on {drive}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error emptying recycle bin: {ex.Message}");
            }
        });

        result.Success = result.Errors.Count == 0;
        return result;
    }

    public static async Task<CleanResult> CleanAll()
    {
        CleanResult result = new CleanResult();

        var prefetchResult = await RemovePrefetchFiles();
        var recycleResult = await EmptyRecycleBin();

        result.FilesDeleted = prefetchResult.FilesDeleted + recycleResult.FilesDeleted;
        result.FoldersDeleted = recycleResult.FoldersDeleted;
        result.Errors.AddRange(prefetchResult.Errors);
        result.Errors.AddRange(recycleResult.Errors);
        result.Success = result.Errors.Count == 0;

        return result;
    }
}

