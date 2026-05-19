namespace DoipSimulator.Core.Ecu;

public sealed record SecurityAccessLevelSnapshot(
    int Level,
    bool IsUnlocked,
    int FailedAttempts,
    DateTimeOffset? LockedUntil,
    bool HasSeed);

public sealed record TesterPresentTimingSnapshot(
    bool TimeoutEnabled,
    int TimeoutMs,
    DateTimeOffset? LastAcceptedAt,
    DateTimeOffset? TimeoutDeadline,
    DateTimeOffset? LastFallbackAt,
    string? LastFallbackReason,
    string? LastFallbackPreviousSession,
    string CurrentSession);

public sealed record TesterPresentTimeoutResult(
    bool FellBack,
    DiagnosticSession PreviousSession,
    DiagnosticSession CurrentSession,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset? LastAcceptedAt,
    DateTimeOffset? TimeoutDeadline,
    string Reason);

public sealed record FlashDownloadSnapshot(
    bool IsActive,
    bool IsCompleted,
    uint MemoryAddress,
    int TotalSize,
    int ReceivedSize,
    int MaxBlockLength,
    byte ExpectedBlockSequenceCounter,
    byte DataFormatIdentifier,
    byte AddressAndLengthFormatIdentifier);

public enum DiagnosticSession
{
    Default = 0x01,
    Programming = 0x02,
    Extended = 0x03,
}

public sealed class EcuRuntimeState
{
    private readonly Lock gate = new();

    public EcuRuntimeState(ushort logicalAddress)
    {
        LogicalAddress = logicalAddress;
    }

    public ushort LogicalAddress { get; }

    public string SecurityStateSummary
    {
        get
        {
            lock (gate)
            {
                var unlocked = securityLevels
                    .Where(item => item.Value.IsUnlocked)
                    .Select(item => item.Key)
                    .Order()
                    .ToArray();

                return unlocked.Length == 0
                    ? "locked"
                    : "unlocked";
            }
        }
    }

    public DiagnosticSession CurrentSession
    {
        get
        {
            lock (gate)
            {
                return currentSession;
            }
        }
    }

    public DateTimeOffset? LastTesterPresentAt
    {
        get
        {
            lock (gate)
            {
                return lastTesterPresentAt;
            }
        }
    }

    private DiagnosticSession currentSession = DiagnosticSession.Default;
    private DateTimeOffset? lastTesterPresentAt;
    private DateTimeOffset? testerPresentTimeoutDeadline;
    private DateTimeOffset? lastTesterPresentFallbackAt;
    private string? lastTesterPresentFallbackReason;
    private DiagnosticSession? lastTesterPresentFallbackPreviousSession;
    private readonly Dictionary<int, SecurityAccessLevelState> securityLevels = [];
    private FlashDownloadState? flashDownload;

    public DiagnosticSession SetSession(DiagnosticSession session)
    {
        lock (gate)
        {
            var previous = currentSession;
            currentSession = session;
            return previous;
        }
    }

    public void RecordTesterPresent(DateTimeOffset acceptedAt)
    {
        lock (gate)
        {
            lastTesterPresentAt = acceptedAt;
        }
    }

    public void RecordTesterPresent(DateTimeOffset acceptedAt, TimeSpan timeout)
    {
        lock (gate)
        {
            lastTesterPresentAt = acceptedAt;
            testerPresentTimeoutDeadline = acceptedAt.Add(timeout);
        }
    }

    public TesterPresentTimeoutResult EvaluateTesterPresentTimeout(
        bool enabled,
        TimeSpan timeout,
        DateTimeOffset now)
    {
        lock (gate)
        {
            if (!enabled || currentSession == DiagnosticSession.Default)
            {
                return NoTesterPresentFallback(now, "not-applicable");
            }

            var deadline = testerPresentTimeoutDeadline
                ?? (lastTesterPresentAt?.Add(timeout) ?? now.Add(-timeout));
            if (deadline > now)
            {
                return NoTesterPresentFallback(now, "within-timeout");
            }

            var previous = currentSession;
            currentSession = DiagnosticSession.Default;
            lastTesterPresentFallbackAt = now;
            lastTesterPresentFallbackReason = "tester-present-timeout";
            lastTesterPresentFallbackPreviousSession = previous;

            return new TesterPresentTimeoutResult(
                true,
                previous,
                currentSession,
                now,
                lastTesterPresentAt,
                deadline,
                "tester-present-timeout");
        }
    }

    public TesterPresentTimingSnapshot GetTesterPresentTimingSnapshot(bool enabled, int timeoutMs)
    {
        lock (gate)
        {
            return new TesterPresentTimingSnapshot(
                enabled,
                timeoutMs,
                lastTesterPresentAt,
                testerPresentTimeoutDeadline,
                lastTesterPresentFallbackAt,
                lastTesterPresentFallbackReason,
                lastTesterPresentFallbackPreviousSession is null
                    ? null
                    : FormatSession(lastTesterPresentFallbackPreviousSession.Value),
                FormatSession(currentSession));
        }
    }

    public void EnsureSecurityLevel(int level)
    {
        lock (gate)
        {
            if (!securityLevels.ContainsKey(level))
            {
                securityLevels[level] = new SecurityAccessLevelState();
            }
        }
    }

    public SecurityAccessLevelSnapshot GetSecurityLevelSnapshot(int level, DateTimeOffset now)
    {
        lock (gate)
        {
            var state = GetOrCreateSecurityLevel(level);
            return new SecurityAccessLevelSnapshot(
                level,
                state.IsUnlocked,
                state.FailedAttempts,
                IsLockedOut(state, now) ? state.LockedUntil : null,
                state.Seed is not null);
        }
    }

    public IReadOnlyList<SecurityAccessLevelSnapshot> ListSecurityLevelSnapshots(DateTimeOffset now)
    {
        lock (gate)
        {
            return securityLevels
                .OrderBy(item => item.Key)
                .Select(item => new SecurityAccessLevelSnapshot(
                    item.Key,
                    item.Value.IsUnlocked,
                    item.Value.FailedAttempts,
                    IsLockedOut(item.Value, now) ? item.Value.LockedUntil : null,
                    item.Value.Seed is not null))
                .ToArray();
        }
    }

    public byte[] StoreSecuritySeed(int level, byte[] seed)
    {
        lock (gate)
        {
            var state = GetOrCreateSecurityLevel(level);
            state.Seed = [.. seed];
            state.IsUnlocked = false;
            return [.. state.Seed];
        }
    }

    public bool TryGetSecuritySeed(int level, out byte[] seed)
    {
        lock (gate)
        {
            var state = GetOrCreateSecurityLevel(level);
            if (state.Seed is null)
            {
                seed = [];
                return false;
            }

            seed = [.. state.Seed];
            return true;
        }
    }

    public bool IsSecurityLevelUnlocked(int level)
    {
        lock (gate)
        {
            return securityLevels.TryGetValue(level, out var state) && state.IsUnlocked;
        }
    }

    public bool IsAnySecurityLevelUnlocked()
    {
        lock (gate)
        {
            return securityLevels.Values.Any(item => item.IsUnlocked);
        }
    }

    public bool IsSecurityLevelLockedOut(int level, DateTimeOffset now)
    {
        lock (gate)
        {
            return IsLockedOut(GetOrCreateSecurityLevel(level), now);
        }
    }

    public int RecordSecurityKeyFailure(int level, int maxFailedAttempts, TimeSpan lockout, DateTimeOffset now)
    {
        lock (gate)
        {
            var state = GetOrCreateSecurityLevel(level);
            state.IsUnlocked = false;
            state.FailedAttempts++;
            if (state.FailedAttempts >= maxFailedAttempts)
            {
                state.LockedUntil = now.Add(lockout);
            }

            return state.FailedAttempts;
        }
    }

    public void MarkSecurityLevelUnlocked(int level)
    {
        lock (gate)
        {
            var state = GetOrCreateSecurityLevel(level);
            state.IsUnlocked = true;
            state.FailedAttempts = 0;
            state.LockedUntil = null;
        }
    }

    public void ResetSecurityLockoutIfExpired(int level, DateTimeOffset now)
    {
        lock (gate)
        {
            var state = GetOrCreateSecurityLevel(level);
            if (state.LockedUntil is not null && state.LockedUntil <= now)
            {
                state.LockedUntil = null;
                state.FailedAttempts = 0;
            }
        }
    }

    public FlashDownloadSnapshot GetFlashDownloadSnapshot()
    {
        lock (gate)
        {
            return flashDownload?.ToSnapshot() ?? new FlashDownloadSnapshot(
                false,
                false,
                0,
                0,
                0,
                0,
                1,
                0,
                0);
        }
    }

    public bool TryStartFlashDownload(
        uint memoryAddress,
        int totalSize,
        int maxBlockLength,
        byte dataFormatIdentifier,
        byte addressAndLengthFormatIdentifier)
    {
        lock (gate)
        {
            if (flashDownload?.IsActive == true)
            {
                return false;
            }

            flashDownload = new FlashDownloadState
            {
                IsActive = true,
                IsCompleted = false,
                MemoryAddress = memoryAddress,
                TotalSize = totalSize,
                ReceivedSize = 0,
                MaxBlockLength = maxBlockLength,
                ExpectedBlockSequenceCounter = 1,
                DataFormatIdentifier = dataFormatIdentifier,
                AddressAndLengthFormatIdentifier = addressAndLengthFormatIdentifier,
            };

            return true;
        }
    }

    public FlashTransferResult AcceptFlashTransferBlock(byte blockSequenceCounter, int dataLength)
    {
        lock (gate)
        {
            if (flashDownload?.IsActive != true)
            {
                return FlashTransferResult.NoActiveDownload;
            }

            if (blockSequenceCounter != flashDownload.ExpectedBlockSequenceCounter)
            {
                return FlashTransferResult.WrongBlockSequenceCounter;
            }

            if (dataLength <= 0 || dataLength > flashDownload.MaxBlockLength)
            {
                return FlashTransferResult.InvalidBlockLength;
            }

            if (flashDownload.ReceivedSize + dataLength > flashDownload.TotalSize)
            {
                return FlashTransferResult.TotalSizeExceeded;
            }

            flashDownload.ReceivedSize += dataLength;
            flashDownload.ExpectedBlockSequenceCounter = unchecked((byte)(flashDownload.ExpectedBlockSequenceCounter + 1));
            return FlashTransferResult.Accepted;
        }
    }

    public FlashTransferExitResult CompleteFlashDownload()
    {
        lock (gate)
        {
            if (flashDownload?.IsActive != true)
            {
                return FlashTransferExitResult.NoActiveDownload;
            }

            if (flashDownload.ReceivedSize != flashDownload.TotalSize)
            {
                return FlashTransferExitResult.IncompleteTransfer;
            }

            flashDownload.IsActive = false;
            flashDownload.IsCompleted = true;
            return FlashTransferExitResult.Completed;
        }
    }

    public bool ClearFlashDownload()
    {
        lock (gate)
        {
            if (flashDownload is null)
            {
                return false;
            }

            var hadState = flashDownload.IsActive || flashDownload.IsCompleted;
            flashDownload = null;
            return hadState;
        }
    }

    private SecurityAccessLevelState GetOrCreateSecurityLevel(int level)
    {
        if (!securityLevels.TryGetValue(level, out var state))
        {
            state = new SecurityAccessLevelState();
            securityLevels[level] = state;
        }

        return state;
    }

    private static bool IsLockedOut(SecurityAccessLevelState state, DateTimeOffset now)
    {
        return state.LockedUntil is not null && state.LockedUntil > now;
    }

    private TesterPresentTimeoutResult NoTesterPresentFallback(DateTimeOffset now, string reason)
    {
        return new TesterPresentTimeoutResult(
            false,
            currentSession,
            currentSession,
            now,
            lastTesterPresentAt,
            testerPresentTimeoutDeadline,
            reason);
    }

    private static string FormatSession(DiagnosticSession session)
    {
        return session switch
        {
            DiagnosticSession.Default => "default",
            DiagnosticSession.Programming => "programming",
            DiagnosticSession.Extended => "extended",
            _ => session.ToString().ToLowerInvariant(),
        };
    }

    private sealed class SecurityAccessLevelState
    {
        public byte[]? Seed { get; set; }

        public bool IsUnlocked { get; set; }

        public int FailedAttempts { get; set; }

        public DateTimeOffset? LockedUntil { get; set; }
    }

    private sealed class FlashDownloadState
    {
        public bool IsActive { get; set; }

        public bool IsCompleted { get; set; }

        public uint MemoryAddress { get; set; }

        public int TotalSize { get; set; }

        public int ReceivedSize { get; set; }

        public int MaxBlockLength { get; set; }

        public byte ExpectedBlockSequenceCounter { get; set; }

        public byte DataFormatIdentifier { get; set; }

        public byte AddressAndLengthFormatIdentifier { get; set; }

        public FlashDownloadSnapshot ToSnapshot()
        {
            return new FlashDownloadSnapshot(
                IsActive,
                IsCompleted,
                MemoryAddress,
                TotalSize,
                ReceivedSize,
                MaxBlockLength,
                ExpectedBlockSequenceCounter,
                DataFormatIdentifier,
                AddressAndLengthFormatIdentifier);
        }
    }
}

public enum FlashTransferResult
{
    Accepted,
    NoActiveDownload,
    WrongBlockSequenceCounter,
    InvalidBlockLength,
    TotalSizeExceeded,
}

public enum FlashTransferExitResult
{
    Completed,
    NoActiveDownload,
    IncompleteTransfer,
}
