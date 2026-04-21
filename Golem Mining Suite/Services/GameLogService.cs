using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Golem_Mining_Suite.Models;
using Golem_Mining_Suite.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Tails the Star Citizen <c>Game.log</c> file and emits typed <see cref="GameLogEvent"/>s
    /// for session-bracketing concerns (login, QT, death, ship destruction, actor stall,
    /// disconnect, log rotation).
    /// <para>
    /// Implementation details — see <c>tasks/research/R2-game-log-mining.md</c> for the full
    /// rationale:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Opens the file with <c>FileShare.ReadWrite | FileShare.Delete</c> so
    ///     Star Citizen retains full ownership and we don't block its own logger or its rename
    ///     to <c>logbackups\</c> on relaunch.</description></item>
    ///   <item><description>Seeks to end on attach to avoid flooding downstream consumers with
    ///     historical events.</description></item>
    ///   <item><description>Polls every 250 ms — the sweet spot cited by AutoTrackR2 /
    ///     all-slain.</description></item>
    ///   <item><description>Detects rotation via <c>FileInfo.Length</c> decrease or open
    ///     failure; reopens from byte 0 after emitting a <see cref="LogRotationEvent"/> so the
    ///     next <see cref="SessionStartEvent"/> anchors the new session.</description></item>
    /// </list>
    /// <para>
    /// Scope is session management, not mining telemetry. Rock-scan composition, laser fire,
    /// fracture outcome, mined cargo quantity, and refinery orders are NOT in <c>Game.log</c>
    /// post-4.0.2 and must be captured via OCR.
    /// </para>
    /// </summary>
    public sealed class GameLogService : IGameLogService, IAsyncDisposable
    {
        private const int PollIntervalMs = 250;
        private const int OpenRetryInitialMs = 100;
        private const int OpenRetryMaxMs = 2000;
        private const int OpenRetryMaxElapsedMs = 5000;

        private readonly ILogger<GameLogService> _logger;
        private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

        private CancellationTokenSource? _cts;
        private Task? _tailTask;
        private string? _currentLogPath;
        private bool _isTailing;

        /// <summary>Tracks whether we've seen a QT start without a matching QT end.</summary>
        private bool _qtInProgress;

        /// <summary>Last login handle seen — used to filter <c>Actor Death</c> for the local player only.</summary>
        private string? _lastLoginHandle;

        /// <inheritdoc />
        public event EventHandler<GameLogEvent>? GameEventReceived;

        /// <inheritdoc />
        public event EventHandler<bool>? TailingStatusChanged;

        /// <inheritdoc />
        public string? CurrentLogPath => _currentLogPath;

        /// <inheritdoc />
        public bool IsTailing => _isTailing;

        public GameLogService(ILogger<GameLogService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_tailTask is not null && !_tailTask.IsCompleted)
                {
                    _logger.LogDebug("GameLogService.StartAsync called while tailing task is already running; no-op.");
                    return;
                }

                var logPath = ResolveLogPath();
                if (logPath is null)
                {
                    _logger.LogWarning(
                        "Star Citizen Game.log not found at the default LIVE location. " +
                        "GameLogService is disarmed. Install SC or configure an override path to enable log tailing.");
                    _currentLogPath = null;
                    _isTailing = false;
                    return;
                }

                _currentLogPath = logPath;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var token = _cts.Token;
                _tailTask = Task.Run(() => TailLoopAsync(logPath, token), token);
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            await _lifecycleLock.WaitAsync().ConfigureAwait(false);
            CancellationTokenSource? cts;
            Task? tail;
            try
            {
                cts = _cts;
                tail = _tailTask;
                _cts = null;
                _tailTask = null;
            }
            finally
            {
                _lifecycleLock.Release();
            }

            if (cts is null)
            {
                return;
            }

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogDebug(ex, "CancellationTokenSource already disposed during StopAsync.");
            }

            if (tail is not null)
            {
                try
                {
                    await tail.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GameLogService tail task faulted during stop.");
                }
            }

            cts.Dispose();
            SetTailingStatus(false);
        }

        /// <summary>
        /// Resolve the path to <c>Game.log</c>. Strategy:
        /// <list type="number">
        ///   <item><description>Probe the default LIVE install under Program Files.</description></item>
        ///   <item><description>If not present, return <c>null</c> (service stays disarmed).</description></item>
        /// </list>
        /// An override via <see cref="ISettingsService"/> is intentionally not wired yet — the
        /// interface doesn't expose a game-path property and extending it is out of scope for
        /// this task.
        /// </summary>
        private static string? ResolveLogPath()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                var candidate = Path.Combine(programFiles, "Roberts Space Industries", "StarCitizen", "LIVE", "Game.log");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private async Task TailLoopAsync(string logPath, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                FileStream? fs = null;
                StreamReader? reader = null;
                try
                {
                    (fs, reader) = await OpenForTailingAsync(logPath, ct).ConfigureAwait(false);
                    if (fs is null || reader is null)
                    {
                        // Open failed — back off and retry the outer loop.
                        await Task.Delay(OpenRetryMaxMs, ct).ConfigureAwait(false);
                        continue;
                    }

                    // Seek to end so we don't flood consumers with historical lines.
                    fs.Seek(0, SeekOrigin.End);
                    long lastLength = fs.Length;
                    SetTailingStatus(true);
                    EmitEvent(new SessionStartEvent(DateTime.UtcNow, logPath));

                    while (!ct.IsCancellationRequested)
                    {
                        string? line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                        if (line is null)
                        {
                            // EOF — check for rotation before sleeping.
                            if (DetectRotation(logPath, ref lastLength))
                            {
                                EmitEvent(new LogRotationEvent(DateTime.UtcNow, logPath));
                                break; // Outer loop will reopen.
                            }

                            try
                            {
                                await Task.Delay(PollIntervalMs, ct).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }
                            continue;
                        }

                        ParseAndEmit(line);

                        // Update last-known length opportunistically so rotation detection stays accurate.
                        try
                        {
                            lastLength = fs.Length;
                        }
                        catch (IOException ex)
                        {
                            _logger.LogDebug(ex, "Unable to query log file length during tail; will retry.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "I/O error tailing {LogPath}; will reopen after backoff.", logPath);
                    try
                    {
                        await Task.Delay(OpenRetryMaxMs, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in GameLogService tail loop; will reopen after backoff.");
                    try
                    {
                        await Task.Delay(OpenRetryMaxMs, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                finally
                {
                    reader?.Dispose();
                    fs?.Dispose();
                    SetTailingStatus(false);
                }
            }
        }

        /// <summary>
        /// Open the log file with share flags compatible with Star Citizen's own logger
        /// (<c>ReadWrite | Delete</c>). Retries briefly on <see cref="IOException"/> so we don't
        /// lose to a transient write-lock during SC startup.
        /// </summary>
        private async Task<(FileStream?, StreamReader?)> OpenForTailingAsync(string logPath, CancellationToken ct)
        {
            var delayMs = OpenRetryInitialMs;
            var elapsed = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!File.Exists(logPath))
                    {
                        _logger.LogDebug("Game.log missing at {LogPath}; waiting for it to reappear.", logPath);
                    }
                    else
                    {
                        var fs = new FileStream(
                            logPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete,
                            bufferSize: 4096,
                            FileOptions.SequentialScan | FileOptions.Asynchronous);

                        var reader = new StreamReader(
                            fs,
                            Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: true);

                        return (fs, reader);
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "Transient IO opening {LogPath}; will retry in {DelayMs} ms.", logPath, delayMs);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Access denied opening {LogPath}; not retrying.", logPath);
                    return (null, null);
                }

                if (elapsed >= OpenRetryMaxElapsedMs)
                {
                    return (null, null);
                }

                try
                {
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return (null, null);
                }
                elapsed += delayMs;
                delayMs = Math.Min(delayMs * 2, OpenRetryMaxMs);
            }

            return (null, null);
        }

        /// <summary>
        /// Detects whether the tailed file has been rotated (truncated to a smaller length, or
        /// deleted). Updates <paramref name="lastLength"/> with the current length when no
        /// rotation is detected.
        /// </summary>
        private static bool DetectRotation(string logPath, ref long lastLength)
        {
            try
            {
                var info = new FileInfo(logPath);
                if (!info.Exists)
                {
                    return true;
                }

                if (info.Length < lastLength)
                {
                    return true;
                }

                lastLength = info.Length;
                return false;
            }
            catch (IOException)
            {
                // Treat any probe failure as "file gone" — the outer loop will retry the open.
                return true;
            }
        }

        /// <summary>
        /// Parse a single log line and, if it matches a known pattern, raise the corresponding
        /// <see cref="GameLogEvent"/>.
        /// </summary>
        private void ParseAndEmit(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            // Extract timestamp up front; non-timestamped lines (the header block on a fresh log)
            // are still matched against patterns that don't require one (e.g. "Disconnect").
            var timestamp = DateTime.UtcNow;
            string payload = line;
            var tsMatch = SCLogPatterns.Timestamp.Match(line);
            if (tsMatch.Success)
            {
                if (DateTime.TryParseExact(
                        tsMatch.Groups["ts"].Value,
                        "yyyy-MM-ddTHH:mm:ss.fff",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var parsed))
                {
                    timestamp = parsed;
                }
                payload = tsMatch.Groups["rest"].Value;
            }

            // Ordering matters: cheapest / most specific matches first.
            if (TryEmitLogin(payload, timestamp)) return;
            if (TryEmitQuantumStart(payload, timestamp)) return;
            if (TryEmitJumpDriveState(payload, timestamp)) return;
            if (TryEmitActorDeath(payload, timestamp)) return;
            if (TryEmitVehicleDestruction(payload, timestamp)) return;
            if (TryEmitActorStall(payload, timestamp)) return;
            if (TryEmitDisconnect(payload, timestamp)) return;
        }

        private bool TryEmitLogin(string payload, DateTime timestamp)
        {
            var m = SCLogPatterns.LegacyLogin.Match(payload);
            if (m.Success)
            {
                var handle = m.Groups["player"].Value;
                _lastLoginHandle = handle;
                EmitEvent(new LoginEvent(timestamp, handle));
                return true;
            }

            m = SCLogPatterns.CharacterLogin.Match(payload);
            if (m.Success)
            {
                var name = m.Groups["name"].Value;
                _lastLoginHandle = name;
                EmitEvent(new LoginEvent(timestamp, name));
                return true;
            }

            return false;
        }

        private bool TryEmitQuantumStart(string payload, DateTime timestamp)
        {
            var m = SCLogPatterns.QuantumStart.Match(payload);
            if (!m.Success)
            {
                return false;
            }

            _qtInProgress = true;
            EmitEvent(new QtStartEvent(timestamp, m.Groups["who"].Value.Trim()));
            return true;
        }

        private bool TryEmitJumpDriveState(string payload, DateTime timestamp)
        {
            var m = SCLogPatterns.JumpDriveState.Match(payload);
            if (!m.Success)
            {
                return false;
            }

            // QT end is inferred from a transition to Idle after a prior QT start.
            var state = m.Groups["state"].Value;
            if (_qtInProgress && string.Equals(state, "Idle", StringComparison.OrdinalIgnoreCase))
            {
                _qtInProgress = false;
                EmitEvent(new QtEndEvent(timestamp));
                return true;
            }

            // Otherwise the Jump Drive line is informational (ship identity). Not emitted in
            // this scope — kept as a hook for future work.
            return false;
        }

        private bool TryEmitActorDeath(string payload, DateTime timestamp)
        {
            var m = SCLogPatterns.ActorDeath.Match(payload);
            if (!m.Success)
            {
                return false;
            }

            var victim = m.Groups["victim"].Value;

            // Per R2: since 4.0.2 only the local player's events are in the log, so any Actor
            // Death line is effectively the local player. As a belt-and-braces filter, if we've
            // recorded a login handle and the victim doesn't match, skip.
            if (!string.IsNullOrEmpty(_lastLoginHandle) &&
                !string.Equals(victim, _lastLoginHandle, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var reason = m.Groups["damageType"].Value;
            EmitEvent(new PlayerDeathEvent(timestamp, victim, reason));
            return true;
        }

        private bool TryEmitVehicleDestruction(string payload, DateTime timestamp)
        {
            var m = SCLogPatterns.VehicleDestruction.Match(payload);
            if (!m.Success)
            {
                return false;
            }

            if (!int.TryParse(m.Groups["levelTo"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var levelTo))
            {
                return false;
            }

            var vehicle = m.Groups["vehicle"].Value;
            EmitEvent(new ShipDestroyedEvent(timestamp, vehicle, levelTo));
            return true;
        }

        private bool TryEmitActorStall(string payload, DateTime timestamp)
        {
            var m = SCLogPatterns.ActorStall.Match(payload);
            if (!m.Success)
            {
                return false;
            }

            var context = $"Player={m.Groups["player"].Value}, Direction={m.Groups["direction"].Value}, Length={m.Groups["seconds"].Value}s";
            EmitEvent(new ActorStallEvent(timestamp, context));
            return true;
        }

        private bool TryEmitDisconnect(string payload, DateTime timestamp)
        {
            if (SCLogPatterns.Disconnect.IsMatch(payload) ||
                SCLogPatterns.SystemQuit.IsMatch(payload) ||
                SCLogPatterns.EndSession.IsMatch(payload))
            {
                EmitEvent(new DisconnectEvent(timestamp));
                return true;
            }

            return false;
        }

        private void EmitEvent(GameLogEvent evt)
        {
            var handler = GameEventReceived;
            if (handler is null)
            {
                return;
            }

            try
            {
                handler(this, evt);
            }
            catch (Exception ex)
            {
                // Don't let a subscriber exception kill the tail loop.
                _logger.LogError(ex, "GameLogService subscriber threw while handling {EventType}.", evt.GetType().Name);
            }
        }

        private void SetTailingStatus(bool value)
        {
            if (_isTailing == value)
            {
                return;
            }

            _isTailing = value;
            try
            {
                TailingStatusChanged?.Invoke(this, value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GameLogService TailingStatusChanged subscriber threw.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _lifecycleLock.Dispose();
        }
    }
}
