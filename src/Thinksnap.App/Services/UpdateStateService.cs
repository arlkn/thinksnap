using System.IO;
using System.Text.Json;
using Thinksnap.App.Models;

namespace Thinksnap.App.Services;

public sealed class UpdateStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string statePath;

    public UpdateStateService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Thinksnap",
            "Updates",
            "update-state.json"))
    {
    }

    public UpdateStateService(string statePath)
    {
        this.statePath = statePath;
    }

    public UpdateDownloadState Load()
    {
        try
        {
            return File.Exists(statePath)
                ? JsonSerializer.Deserialize<UpdateDownloadState>(File.ReadAllText(statePath), JsonOptions) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }

    public void Save(UpdateDownloadState state)
    {
        var directory = Path.GetDirectoryName(statePath);
        if (string.IsNullOrWhiteSpace(directory) is false)
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = statePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporaryPath, statePath, true);
    }

    public void Clear()
    {
        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }
    }
}
