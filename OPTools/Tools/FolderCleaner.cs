using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OPTools.Tools;

public static class FolderCleaner
{
    public static async Task<CleanResult> CleanFolderContents(string folderPath)
    {
        CleanResult result = new CleanResult();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            result.Errors.Add("Folder path is empty");
            return result;
        }

        if (!Directory.Exists(folderPath))
        {
            result.Errors.Add($"Folder not found: {folderPath}");
            return result;
        }

        try
        {
            await Task.Run(() =>
            {
                DirectoryInfo dir = new DirectoryInfo(folderPath);

                // Delete all files
                foreach (FileInfo file in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    try
                    {
                        file.Attributes = FileAttributes.Normal;
                        file.Delete();
                        result.FilesDeleted++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to delete file {file.FullName}: {ex.Message}");
                    }
                }

                // Delete all subdirectories
                foreach (DirectoryInfo subDir in dir.GetDirectories("*", SearchOption.TopDirectoryOnly).OrderByDescending(d => d.FullName.Length))
                {
                    try
                    {
                        subDir.Attributes = FileAttributes.Normal;
                        subDir.Delete(true);
                        result.FoldersDeleted++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to delete folder {subDir.FullName}: {ex.Message}");
                    }
                }
            });

            result.Success = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error cleaning folder: {ex.Message}");
        }

        return result;
    }
}

public class CleanResult
{
    public bool Success { get; set; }
    public int FilesDeleted { get; set; }
    public int FoldersDeleted { get; set; }
    public System.Collections.Generic.List<string> Errors { get; set; } = new System.Collections.Generic.List<string>();
}

