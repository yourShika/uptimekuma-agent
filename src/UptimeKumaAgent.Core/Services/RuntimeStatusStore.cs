using System.Text.Json;
using UptimeKumaTrayAgent.Models;

namespace UptimeKumaTrayAgent.Services;

/// <summary>
/// A snapshot of the live monitoring state that is shared between the background
/// Windows service (which performs the scheduled checks and pushes) and any GUI /
/// tray process (which only visualizes it). The service is the authoritative writer;
/// GUI processes read the file so their dashboard reflects the real, running state
/// instead of their own idle in-process monitoring instance.
/// </summary>
public sealed class RuntimeStatusSnapshot
{
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public int ProcessId { get; set; }
    public string Source { get; set; } = "";
    public bool IsRunning { get; set; }
    public DateTimeOffset? LastRun { get; set; }
    public DateTimeOffset? LastSuccessfulCheck { get; set; }
    public string LastError { get; set; } = "";
    public List<CheckRuntimeStatus> Statuses { get; set; } = new();
}

public sealed class RuntimeStatusStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    private readonly string _path;
    private readonly object _writeLock = new();

    public RuntimeStatusStore(IAgentPaths paths)
    {
        _path = System.IO.Path.Combine(paths.DataDirectory, "runtime-status.json");
    }

    public string FilePath => _path;

    /// <summary>Writes the snapshot atomically (temp file + move) so readers never observe a partial file.</summary>
    public void Write(RuntimeStatusSnapshot snapshot)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot, Options);
            lock (_writeLock)
            {
                var tempPath = _path + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _path, overwrite: true);
            }
        }
        catch
        {
            // The snapshot is a best-effort convenience for the dashboard; failing to
            // persist it must never affect the monitoring loop itself.
        }
    }

    public RuntimeStatusSnapshot? Read()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<RuntimeStatusSnapshot>(json, Options);
        }
        catch
        {
            return null;
        }
    }
}
