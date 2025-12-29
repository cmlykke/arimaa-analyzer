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
    private readonly TimeSpan _searchTimeout = TimeSpan.FromSeconds(5);
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private readonly ConcurrentQueue<string> _stderrBuffer = new();
    private readonly List<string> _capabilities = new();
    private readonly Dictionary<string, string> _currentOptionValues = new(StringComparer.OrdinalIgnoreCase);

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

        while ((line = await ReadLineAsync(ct, _defaultTimeout).ConfigureAwait(false)) != null)
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
    {
        _currentOptionValues[name] = value;
        return SendAsync($"setoption name {name} value {value}", ct);
    }

    /// <summary>
    /// Waits for the engine to finish initialization and be ready to accept commands.
    /// </summary>
    public async Task IsReadyAsync(CancellationToken ct = default)
    {
        await SendAsync("isready", ct).ConfigureAwait(false);
        string? line;
        while ((line = await ReadLineAsync(ct, _defaultTimeout).ConfigureAwait(false)) != null)
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
        // Use an overall search timeout instead of a per-line timeout, since engines may be silent during search.
        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        overallCts.CancelAfter(GetSearchTimeout());
        while (true)
        {
            // Drain any pending stderr lines first, engines sometimes send important info there
            while (_stderrBuffer.TryDequeue(out var errLine))
            {
                log.Add($"[stderr] {errLine}");
                if (TryParseBestmoveLine(errLine, out var b, out var p)) { best = b; ponder = p; break; }
            }
            if (!string.IsNullOrEmpty(best))
                break;

            try
            {
                line = await ReadLineAsync(overallCts.Token, timeout: null).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                var sample = string.Join("\n", log.TakeLast(20));
                throw new TimeoutException($"Engine did not return bestmove within {GetSearchTimeout().TotalSeconds:F1}s. Last output lines:\n{sample}");
            }

            if (line is null)
                continue;

            log.Add(line);
            if (TryParseBestmoveLine(line, out var b2, out var p2)) { best = b2; ponder = p2; break; }
        }

        if (string.IsNullOrEmpty(best))
            throw new InvalidOperationException("Engine did not return bestmove.");

        return (best!, ponder, log);
    }

    private static bool TryParseBestmoveLine(string line, out string bestMove, out string? ponder)
    {
        bestMove = string.Empty;
        ponder = null;
        const string prefix = "bestmove ";
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        // Typical formats seen in AEI engines:
        // 1) bestmove <step1> <step2> <step3> <step4>
        // 2) bestmove <step...> ponder <move>
        var afterKeyword = line.Substring(prefix.Length).Trim();
        const string ponderMarker = " ponder ";
        var ponderIndex = afterKeyword.IndexOf(ponderMarker, StringComparison.OrdinalIgnoreCase);
        if (ponderIndex >= 0)
        {
            bestMove = afterKeyword.Substring(0, ponderIndex).Trim();
            var ponderPart = afterKeyword.Substring(ponderIndex + ponderMarker.Length).Trim();
            if (!string.IsNullOrWhiteSpace(ponderPart))
            {
                var spaceIdx = ponderPart.IndexOf(' ');
                ponder = spaceIdx > 0 ? ponderPart.Substring(0, spaceIdx) : ponderPart;
            }
        }
        else
        {
            bestMove = afterKeyword;
        }
        return !string.IsNullOrEmpty(bestMove);
    }

    private TimeSpan GetSearchTimeout()
    {
        // If caller set a per-move time via option 'tcmove', scale timeout accordingly.
        if (_currentOptionValues.TryGetValue("tcmove", out var val) && int.TryParse(val, out var seconds) && seconds > 0)
        {
            // Allow generous slack over engine's per-move time because sharp2015 may exceed on the first move.
            // 3x tcmove + 5s buffer, bounded below by baseline.
            var dyn = TimeSpan.FromSeconds(seconds * 3 + 5);
            // Ensure at least the baseline timeout
            return dyn < _searchTimeout ? _searchTimeout : dyn;
        }
        return _searchTimeout;
    }

    public Task StopAsync(CancellationToken ct = default) => SendAsync("stop", ct);

    public async Task QuitAsync()
    {
        try { await SendAsync("quit"); } catch { /* ignored */ }
        try { _process?.WaitForExit(2000); } catch { /* ignored */ }
        Cleanup();
    }

    /// <summary>
    /// Sets the position and an engine option, then returns the search results.
    /// </summary>
    public async Task<(string bestMove, string? ponder, IReadOnlyList<string> log)> GetBestMoveAsync(
        string aeiPosition, 
        string optionName, 
        string optionValue, 
        CancellationToken ct = default)
    {
        //the AI can only make gold moves, so if it silver to move, the board must be flipped:
        var AEIflipped = AeiPerspectiveService.ensureMoverAtBottom(aeiPosition);
        
        //var notflippedBoard = NotationService.AeiToBoard(aeiPosition);
        //var flippedBoard = NotationService.AeiToBoard(AEIflipped);
        
        await SendAsync(AEIflipped, ct).ConfigureAwait(false);
        await SetOptionAsync(optionName, optionValue, ct).ConfigureAwait(false);
        await IsReadyAsync(ct).ConfigureAwait(false);

        var resultmoves = await GoAsync(string.Empty, ct).ConfigureAwait(false);

        if (AeiPerspectiveService.GetSideToMove(aeiPosition) == 'g')
        {
            return resultmoves;
        }
        else
        {
            var flipmove = AeiPerspectiveService.ConvertGoldMoveToSilver(resultmoves.bestMove);
            return (flipmove, resultmoves.ponder, resultmoves.log);
        }
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

    private async Task<string?> ReadLineAsync(CancellationToken ct, TimeSpan? timeout = null)
    {
        if (_stdout is null) throw new InvalidOperationException("Engine stdout not available.");

        var readTask = _stdout.ReadLineAsync();
        if (timeout is null || timeout.Value <= TimeSpan.Zero)
        {
            // No per-line timeout, but still respect cancellation.
            var completed = await Task.WhenAny(readTask, Task.Delay(Timeout.InfiniteTimeSpan, ct)).ConfigureAwait(false);
            if (completed == readTask)
                return await readTask.ConfigureAwait(false);
            // Cancellation requested
            ct.ThrowIfCancellationRequested();
            return null; // unreachable
        }

        var delayTask = Task.Delay(timeout.Value, ct);
        var completedTimed = await Task.WhenAny(readTask, delayTask).ConfigureAwait(false);

        if (completedTimed == readTask)
            return await readTask.ConfigureAwait(false);

        throw new TimeoutException($"Timed out reading from engine after {timeout.Value.TotalSeconds:F1}s.");
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

    /// <summary>
    /// Builds a linear main-line GameTurn tree by repeatedly asking the AEI engine for a best move.
    /// The first returned node is the first move generated from the provided AEI position.
    /// </summary>
    /// <param name="positionNode">A GameTurn representing the starting position; its <see cref="GameTurn.AEIstring"/> will be used.</param>
    /// <param name="searchDepth">Number of consecutive turns to generate. Must be greater than 0.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The root GameTurn node of the generated chain (depth nodes long).</returns>
    public async Task<GameTurn> BuildGameTurnTreeAsync(GameTurn positionNode, int searchDepth, CancellationToken ct = default)
    {
        if (positionNode is null) throw new ArgumentNullException(nameof(positionNode));
        if (searchDepth <= 0) throw new ArgumentOutOfRangeException(nameof(searchDepth), "searchDepth must be > 0");

        // Start engine if necessary (attempt to auto-resolve bundled path)
        if (!IsRunning)
        {
            var exePath = ResolveSharp2015Path();
            await StartAsync(exePath, arguments: "aei", ct: ct).ConfigureAwait(false);
        }

        await NewGameAsync(ct).ConfigureAwait(false);

        GameTurn? root = null;
        GameTurn? tail = null;
        string currentAei = positionNode.AEIstring;
        
        // Derive a numeric move number if possible; default to 1
        int moveNumber = 1;
        if (int.TryParse(positionNode.MoveNumber, out var parsed))
            moveNumber = parsed + 1;

        for (int i = 0; i < searchDepth; i++)
        {
            // Use GetBestMoveAsync to handle position, options, and getting the move
            var (bestMove, _, _) = await GetBestMoveAsync(currentAei, "tcmove", "2", ct).ConfigureAwait(false);
            
            var moves = bestMove.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var side = ParseSideFromAei(currentAei);

            var node = new GameTurn(
                oldAEIstring: currentAei,
                updatedAEIstring: string.Empty,
                MoveNumber: moveNumber.ToString(),
                Side: side,
                Moves: moves,
                isMainLine: false);

            if (root is null)
            {
                root = node;
            }
            else
            {
                tail!.AddChild(node);
            }
            tail = node;

            // Advance AEI for the next iteration
            currentAei = NotationService.GamePlusMovesToAei(currentAei, moves);
            moveNumber++;
        }

        return root!;
    }

    private static Sides ParseSideFromAei(string aei)
    {
        var idx = aei.IndexOf("setposition", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) throw new ArgumentException("Invalid AEI: missing 'setposition'.");
        var after = aei.Substring(idx + "setposition".Length).TrimStart();
        if (after.Length == 0) throw new ArgumentException("Invalid AEI: missing side code.");
        var sideCode = after[0];
        return sideCode is 'g' or 'G' ? Sides.Gold
             : sideCode is 's' or 'S' ? Sides.Silver
             : throw new ArgumentException($"Invalid AEI side code: {sideCode}");
    }

    private static string ResolveSharp2015Path()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = System.IO.Path.Combine(baseDir, "Aiexecutables", "sharp2015.exe");
        if (System.IO.File.Exists(candidate)) return candidate;

        var candidate2 = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            baseDir, "..", "..", "..", "..", "ArimaaAnalyzer.Maui", "Aiexecutables", "sharp2015.exe"));
        if (System.IO.File.Exists(candidate2)) return candidate2;

        throw new FileNotFoundException("Could not locate sharp2015.exe under Aiexecutables.");
    }
}

public record AeiOption(string Name, string Type, string? DefaultValue);