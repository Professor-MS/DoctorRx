using System;
using System.Collections.Generic;
using System.IO;
using DoctorRx.Application.Interfaces;

namespace DoctorRx.Infrastructure.Services;

public class PhysicalFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void CopyFile(string source, string destination, bool overwrite) =>
        File.Copy(source, destination, overwrite);

    public void MoveFile(string source, string destination, bool overwrite)
    {
        if (overwrite && File.Exists(destination))
        {
            File.Delete(destination);
        }
        File.Move(source, destination);
    }

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public long GetAvailableFreeSpace(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root))
            {
                var driveInfo = new DriveInfo(root);
                if (driveInfo.IsReady)
                {
                    return driveInfo.AvailableFreeSpace;
                }
            }
        }
        catch
        {
            // If drive cannot be resolved (e.g. non-standard path), return a large value or fallback
        }

        return long.MaxValue;
    }

    public Stream OpenRead(string path) =>
        File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    public Stream OpenWrite(string path) =>
        File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

    public Stream CreateFile(string path) =>
        File.Create(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void WriteAllText(string path, string content) => File.WriteAllText(path, content);

    public IEnumerable<string> GetFiles(string path, string searchPattern) =>
        Directory.Exists(path) ? Directory.GetFiles(path, searchPattern) : Array.Empty<string>();

    public DateTime GetCreationTimeUtc(string path) =>
        File.GetCreationTimeUtc(path);
}
