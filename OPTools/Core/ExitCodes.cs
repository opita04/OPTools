using System;

namespace OPTools.Core;

/// <summary>
/// Exit codes for silent mode operation.
/// Used for automation scripts and command-line integration.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Operation completed successfully.
    /// </summary>
    public const int Success = 0;
    
    /// <summary>
    /// General failure (unspecified error).
    /// </summary>
    public const int GeneralFailure = 1;
    
    /// <summary>
    /// Access denied - dangerous path blocked.
    /// </summary>
    public const int AccessDenied = 2;
    
    /// <summary>
    /// Invalid path specified.
    /// </summary>
    public const int InvalidPath = 3;
    
    /// <summary>
    /// Failed to release file locks.
    /// </summary>
    public const int LockReleaseFailed = 4;
    
    /// <summary>
    /// Failed to delete file or folder.
    /// </summary>
    public const int DeleteFailed = 5;
}
