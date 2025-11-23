using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// AEI (Arimaa Engine Interface) controller service.
/// Manages a child engine process and speaks the AEI protocol over stdin/stdout.
/// Reference: https://github.com/Janzert/AEI/blob/master/aei-protocol.txt
/// </summary>
public class AnalysisService : IAsyncDisposable
{
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    // Increase timeout as some engines might take time to load neural nets or large tables
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly ConcurrentQueue<string> _stderrBuffer = new();
    private readonly List<string> _capabilities = new();

    public string? EngineName { get; private set; }
    public string? EngineAuthor { get; private set; }
    public IReadOnlyDictionary<string, AeiOption> Options => _options;
    private readonly Dictionary<string, AeiOption> _options = new(StringComparer.OrdinalIgnoreCase);

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// Starts the engine process and performs the AEI handshake (aei ... aeiok).
    /// </summary>
    public async Task StartAsync(string exePath, string? workingDirectory = null, string arguments = "", CancellationToken ct = default)
    {
        if (IsRunning)
            return;

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? System.IO.Path.GetDirectoryName(exePath) ?? string.Empty : workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _stderrBuffer.Enqueue(e.Data); };

        if (!_process.Start())
            throw new InvalidOperationException("Failed to start engine process.");

        _stdin = _process.StandardInput;
        _stdin.AutoFlush = true;
        _stdout = _process.StandardOutput;
        _process.BeginErrorReadLine();

        // Perform AEI handshake
        await SendAsync("aei", ct).ConfigureAwait(false);
        await ReadAeiHandshakeAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse AEI handshake (id, option, aeiok). Populates EngineName, EngineAuthor, Options.
    /// </summary>
    private async Task ReadAeiHandshakeAsync(CancellationToken ct)
    {
        if (_stdout is null) throw new InvalidOperationException("Engine stdout not available.");

        string? line;
        var optionRegex = new Regex("^option\\s+name\\s+(?<name>[^\\s]+)(?:\\s+type\\s+(?<type>[^\\s]+))?(?:\\s+default\\s+(?<def>.+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        while ((line = await ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            if (line.StartsWith("id name ", StringComparison.OrdinalIgnoreCase))
            {
                EngineName = line.Substring("id name ".Length).Trim();
            }
            else if (line.StartsWith("id author ", StringComparison.OrdinalIgnoreCase))
            {
                EngineAuthor = line.Substring("id author ".Length).Trim();
            }
            else if (line.StartsWith("option ", StringComparison.OrdinalIgnoreCase))
            {
                var m = optionRegex.Match(line);
                if (m.Success)
                {
                    var name = m.Groups["name"].Value.Trim();
                    var type = m.Groups["type"].Success ? m.Groups["type"].Value.Trim() : "string";
                    var def = m.Groups["def"].Success ? m.Groups["def"].Value.Trim() : null;
                    _options[name] = new AeiOption(name, type, def);
                }
            }
            else if (line.StartsWith("feature ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("capability ", StringComparison.OrdinalIgnoreCase))
            {
                _capabilities.Add(line);
            }
            else if (string.Equals(line, "aeiok", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            // Some engines may echo info lines; we ignore others.
        }
    }

    /// <summary>
    /// Sends a setoption command. Value is sent as-is (no quoting added).
    /// </summary>
    public Task SetOptionAsync(string name, string value, CancellationToken ct = default)
        => SendAsync($"setoption name {name} value {value}", ct);

    /// <summary>
    /// Waits for the engine to finish initialization and be ready to accept commands.
    /// </summary>
    public async Task IsReadyAsync(CancellationToken ct = default)
    {
        await SendAsync("isready", ct).ConfigureAwait(false);
        string? line;
        while ((line = await ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            if (string.Equals(line, "readyok", StringComparison.OrdinalIgnoreCase))
                return;
            // ignore other info lines
        }
        throw new TimeoutException("Engine did not respond with readyok.");
    }

    public Task NewGameAsync(CancellationToken ct = default) => SendAsync("newgame", ct);

    /// <summary>
    /// Sets the position. The exact format is engine dependent but AEI specifies: "position [setup] moves m1 m2 ...".
    /// Provide either a setup string or leave it empty to use start position.
    /// </summary>
    public Task PositionAsync(string setupOrEmpty, IEnumerable<string>? moves = null, CancellationToken ct = default)
    {
        var sb = new StringBuilder("position");
        if (!string.IsNullOrWhiteSpace(setupOrEmpty)) sb.Append(' ').Append(setupOrEmpty);
        if (moves is not null)
        {
            sb.Append(" moves");
            foreach (var m in moves)
                sb.Append(' ').Append(m);
        }
        return SendAsync(sb.ToString(), ct);
    }

    /// <summary>
    /// Starts analysis/search. Returns the bestmove (and optional ponder) reported by the engine.
    /// goParameters e.g. "movetime 5000" or engine-specific args.
    /// </summary>
    public async Task<(string bestMove, string? ponder, IReadOnlyList<string> log)> GoAsync(string goParameters, CancellationToken ct = default)
    {
        var log = new List<string>();
        await SendAsync($"go {goParameters}".Trim(), ct).ConfigureAwait(false);

        string? best = null;
        string? ponder = null;
        string? line;
        // Reading loop until bestmove is found.
        // Note: A robust implementation might need a separate read loop/thread to handle 'info' lines asynchronously.
        while ((line = await ReadLineAsync(ct).ConfigureAwait(false)) != null)
        {
            log.Add(line);
            if (line.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase))
            {
                // Typical formats seen in AEI engines:
                // 1) bestmove <step1> <step2> <step3> <step4>
                // 2) bestmove <step...> ponder <move>
                // We want to capture the full move sequence after 'bestmove ' (until optional ' ponder ').
                var afterKeyword = line.Substring("bestmove ".Length).Trim();
                var ponderMarker = " ponder ";
                var ponderIndex = afterKeyword.IndexOf(ponderMarker, StringComparison.OrdinalIgnoreCase);
                if (ponderIndex >= 0)
                {
                    var bestSeq = afterKeyword.Substring(0, ponderIndex).Trim();
                    var ponderPart = afterKeyword.Substring(ponderIndex + ponderMarker.Length).Trim();
                    // Some engines may include extra tokens after ponder; we take the next token as the ponder move
                    if (!string.IsNullOrWhiteSpace(ponderPart))
                    {
                        var spaceIdx = ponderPart.IndexOf(' ');
                        ponder = spaceIdx > 0 ? ponderPart.Substring(0, spaceIdx) : ponderPart;
                    }
                    best = bestSeq;
                }
                else
                {
                    best = afterKeyword;
                }
                break;
            }
        }

        if (string.IsNullOrEmpty(best))
            throw new InvalidOperationException("Engine did not return bestmove.");

        return (best!, ponder, log);
    }

    public Task StopAsync(CancellationToken ct = default) => SendAsync("stop", ct);

    public async Task QuitAsync()
    {
        try { await SendAsync("quit"); } catch { /* ignored */ }
        try { _process?.WaitForExit(2000); } catch { /* ignored */ }
        Cleanup();
    }

    public async Task SendAsync(string command, CancellationToken ct = default)
    {
        if (_stdin is null) throw new InvalidOperationException("Engine stdin not available.");
        await _ioLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stdin.WriteLineAsync(command).ConfigureAwait(false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        if (_stdout is null) throw new InvalidOperationException("Engine stdout not available.");

        // We use a linked token source so we can enforce a read timeout if the engine hangs
        // Exception: If 'go infinite' is used, this logic needs to be adapted to wait indefinitely.
        // For now, we assume standard command-response behavior.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // Do not cancel via timeout here for 'go' commands in a real app, logic should be split.
        // But for handshake/isready, timeout is useful.
        
        var readTask = _stdout.ReadLineAsync();
        // Wait for read or cancellation
        // Note: ReadLineAsync(CancellationToken) is not available in older .NET Standard, but OK in .NET 6+
        // However, StreamReader.ReadLineAsync() usually doesn't take a token directly.
        // We wrap it:
        var completed = await Task.WhenAny(readTask, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)).ConfigureAwait(false);
        
        if (completed == readTask)
            return await readTask.ConfigureAwait(false);
            
        throw new TimeoutException("Timed out reading from engine.");
    }

    private void Cleanup()
    {
        try { _stdin?.Dispose(); } catch { }
        try { _stdout?.Dispose(); } catch { }
        try { if (_process is { HasExited: false } p) p.Kill(entireProcessTree: true); } catch { }
        try { _process?.Dispose(); } catch { }
        _stdin = null;
        _stdout = null;
        _process = null;
    }

    public ValueTask DisposeAsync()
    {
        Cleanup();
        _ioLock.Dispose();
        return ValueTask.CompletedTask;
    }
}

public record AeiOption(string Name, string Type, string? DefaultValue);