namespace Waves.Diff;

/// <summary>Directory checksum selection; Default matches Haiyu's hpatchz invocation.</summary>
[Flags]
public enum DiffChecksumValidation
{
    None = 0,
    Old = 1,
    New = 2,
    Copy = 4,
    Diff = 8,
    Default = New | Copy,
    All = Old | New | Copy | Diff,
}

/// <summary>Immutable per-operation settings. Obsolete source files remain the installer's responsibility.</summary>
public readonly record struct DiffPatchOptions
{
    private readonly DiffChecksumValidation? _checksums;
    // A default-initialized value also has native-compatible defaults; None is an explicit choice.
    public DiffChecksumValidation Checksums
    {
        get => _checksums ?? DiffChecksumValidation.Default;
        init => _checksums = value;
    }
    public static DiffPatchOptions Default { get; } = new();
    public static DiffPatchOptions Strict { get; } = new() { Checksums = DiffChecksumValidation.All };

    internal void Validate()
    {
        if ((Checksums & ~DiffChecksumValidation.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(Checksums));
    }
}

public enum DiffPatchStage
{
    Patching,
    Committing,
    Completed,
}
