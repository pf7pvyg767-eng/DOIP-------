namespace DoipSimulator.Core.Ecu;

public sealed record SecurityAccessLevelSnapshot(
    int Level,
    bool IsUnlocked,
    int FailedAttempts,
    DateTimeOffset? LockedUntil,
    bool HasSeed);

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
    private readonly Dictionary<int, SecurityAccessLevelState> securityLevels = [];

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

    private sealed class SecurityAccessLevelState
    {
        public byte[]? Seed { get; set; }

        public bool IsUnlocked { get; set; }

        public int FailedAttempts { get; set; }

        public DateTimeOffset? LockedUntil { get; set; }
    }
}
