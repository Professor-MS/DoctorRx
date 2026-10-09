using System;
using System.Collections.Generic;
using System.IO;

namespace DoctorRx.Application.Interfaces;

/// <summary>
/// File system abstraction enabling safe testing of disk full, file locks, and unplugged drives.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    void CopyFile(string source, string destination, bool overwrite);
    void MoveFile(string source, string destination, bool overwrite);
    long GetFileSize(string path);
    long GetAvailableFreeSpace(string path);
    Stream OpenRead(string path);
    Stream OpenWrite(string path);
    Stream CreateFile(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    IEnumerable<string> GetFiles(string path, string searchPattern);
    DateTime GetCreationTimeUtc(string path);
}
