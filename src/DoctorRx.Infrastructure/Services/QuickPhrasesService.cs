using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DoctorRx.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace DoctorRx.Infrastructure.Services;

public class QuickPhrasesService : IQuickPhrasesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly IAppPaths _appPaths;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<QuickPhrasesService>? _logger;
    private readonly string _filePath;

    public QuickPhrasesService(IAppPaths appPaths, IFileSystem fileSystem, ILogger<QuickPhrasesService>? logger = null)
    {
        _appPaths = appPaths;
        _fileSystem = fileSystem;
        _logger = logger;
        _filePath = Path.Combine(_appPaths.BaseDirectory, "quick-phrases.json");
    }

    public Task<IReadOnlyList<string>> GetQuickPhrasesAsync()
    {
        try
        {
            if (_fileSystem.FileExists(_filePath))
            {
                var json = _fileSystem.ReadAllText(_filePath);
                var phrases = JsonSerializer.Deserialize<List<string>>(json);
                if (phrases != null)
                {
                    return Task.FromResult<IReadOnlyList<string>>(phrases);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load quick phrases from {Path}", _filePath);
        }

        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task SaveQuickPhrasesAsync(IEnumerable<string> phrases)
    {
        try
        {
            var list = phrases?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList() ?? new List<string>();
            var json = JsonSerializer.Serialize(list, JsonOptions);
            _fileSystem.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist quick phrases to {Path}", _filePath);
            throw;
        }

        return Task.CompletedTask;
    }
}
