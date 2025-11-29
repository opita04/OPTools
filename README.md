# FolderDelete - Force Delete Locked Files and Folders

A Windows application similar to Unlocker that can forcefully delete folders and files, especially those locked by other processes.

## Features

- **Show Locking Processes**: Display which processes are locking files/folders
- **Kill Processes Option**: Terminate processes that are locking files
- **Silent Force Delete**: Command-line mode for automation
- **Context Menu Integration**: Right-click option in Windows Explorer

## Requirements

- Windows 10/11
- .NET 8.0 Runtime (included in self-contained build)
- Administrator privileges (required for handle enumeration)

## Usage

### GUI Mode
1. Right-click on a file or folder in Windows Explorer
2. Select "Delete with FolderDelete"
3. View locking processes and unlock/kill as needed

### Command-Line Mode
```
FolderDelete.exe "C:\path\to\file" /S
```
- `/S` or `-S`: Silent mode (force delete without showing GUI)

## Building

```bash
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true
```

## Installation

The application requires administrator privileges. To install the context menu:
1. Run the application as administrator
2. The context menu will be registered automatically

