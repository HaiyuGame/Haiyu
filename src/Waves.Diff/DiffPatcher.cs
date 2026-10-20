using System.Text;

namespace Waves.Diff;

public static class DiffPatcher
{
    public static void PatchDirectory(
        string oldPath,
        string diffFile,
        string newPath,
        Action<DiffProgress>? onProgress = null,
        CancellationToken cancellationToken = default
    ) =>
        PatchDirectory(
            oldPath,
            diffFile,
            newPath,
            DiffPatchOptions.Default,
            onProgress,
            cancellationToken
        );

    public static void PatchDirectory(
        string oldPath,
        string diffFile,
        string newPath,
        DiffPatchOptions options,
        Action<DiffProgress>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        ValidateDirectoryMagic(ReadMagic(diffFile));
        Hdiff19Patcher.Patch(oldPath, diffFile, newPath, onProgress, cancellationToken, options);
    }

    public static Task PatchDirectoryAsync(
        string oldPath,
        string diffFile,
        string newPath,
        Action<DiffProgress>? onProgress = null,
        CancellationToken cancellationToken = default
    ) =>
        PatchDirectoryAsync(
            oldPath,
            diffFile,
            newPath,
            DiffPatchOptions.Default,
            onProgress,
            cancellationToken
        );

    public static async Task PatchDirectoryAsync(
        string oldPath,
        string diffFile,
        string newPath,
        DiffPatchOptions options,
        Action<DiffProgress>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        ValidateDirectoryMagic(
            await ReadMagicAsync(diffFile, cancellationToken).ConfigureAwait(false)
        );
        await Hdiff19Patcher
            .PatchAsync(oldPath, diffFile, newPath, onProgress, cancellationToken, options)
            .ConfigureAwait(false);
    }

    public static void PatchFile(
        string oldFile,
        string diffFile,
        string newFile,
        Action<DiffProgress>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDistinctOutput(oldFile, diffFile, newFile);
        using var oldStream = OpenRead(oldFile, false);
        using var diffStream = OpenRead(diffFile, false);
        var info = SingleDiffDispatcher.Inspect(diffStream, 0, diffStream.Length);
        ValidateOldSize(info, oldStream);
        DiffSpeedMeter? speed = onProgress is null ? null : new();
        using (var output = OpenWrite(newFile, false))
        {
            SingleDiffDispatcher.Patch(
                info,
                diffStream,
                diffFile,
                0,
                diffStream.Length,
                oldStream,
                output,
                onProgress == null
                    ? null
                    : bytes => onProgress(FileProgress(info.NewSize, bytes, false, speed!)),
                cancellationToken
            );
            cancellationToken.ThrowIfCancellationRequested();
            output.Flush();
        }
        if (onProgress is not null)
            onProgress(FileProgress(info.NewSize, info.NewSize, true, speed!));
    }

    /// <summary>Async file I/O with cooperative cancellation; decompression and transforms are CPU work.</summary>
    public static async Task PatchFileAsync(
        string oldFile,
        string diffFile,
        string newFile,
        Action<DiffProgress>? onProgress = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureDistinctOutput(oldFile, diffFile, newFile);
        await using var oldStream = OpenRead(oldFile, true);
        await using var diffStream = OpenRead(diffFile, true);
        var info = await SingleDiffDispatcher
            .InspectAsync(diffStream, 0, diffStream.Length, cancellationToken)
            .ConfigureAwait(false);
        ValidateOldSize(info, oldStream);
        DiffSpeedMeter? speed = onProgress is null ? null : new();
        await using (var output = OpenWrite(newFile, true))
        {
            await SingleDiffDispatcher
                .PatchAsync(
                    info,
                    diffStream,
                    diffFile,
                    0,
                    diffStream.Length,
                    oldStream,
                    output,
                    onProgress == null
                        ? null
                        : bytes => onProgress(FileProgress(info.NewSize, bytes, false, speed!)),
                    cancellationToken
                )
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        if (onProgress is not null)
            onProgress(FileProgress(info.NewSize, info.NewSize, true, speed!));
    }

    private static DiffProgress FileProgress(
        ulong total,
        ulong done,
        bool completed,
        DiffSpeedMeter speed
    ) =>
        new()
        {
            Stage = completed ? DiffPatchStage.Completed : DiffPatchStage.Patching,
            FileTotalCount = 1,
            PatchedFileCount = completed ? 1UL : 0,
            PatchingFileCurrentSize = done,
            PatchingFileTotalSize = total,
            PatchedCurrentBytes = done,
            PatchTotalBytes = total,
            SpeedValue = speed.Sample(done, completed),
        };

    private static void ValidateOldSize(SingleDiffDispatcher.Info info, Stream old)
    {
        if ((ulong)old.Length != info.OldSize)
            throw new InvalidDataException("Old file length differs from the patch header.");
    }

    private static FileStream OpenRead(string path, bool asyncIo) =>
        new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.RandomAccess | (asyncIo ? FileOptions.Asynchronous : FileOptions.None)
        );

    private static FileStream OpenWrite(string path, bool asyncIo) =>
        new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.SequentialScan | (asyncIo ? FileOptions.Asynchronous : FileOptions.None)
        );

    private static void ValidateDirectoryMagic(string magic)
    {
        if (magic == "HDIFF19&")
            return;
        if (magic is "HDIFF13&" or "HDIFFSF2")
            throw new NotSupportedException("Use PatchFile for a standalone file patch.");
        throw new InvalidDataException("Expected an HDIFF19 directory patch.");
    }

    private static void EnsureDistinctOutput(string oldFile, string diffFile, string newFile)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string target = Path.GetFullPath(newFile);
        if (
            string.Equals(target, Path.GetFullPath(oldFile), comparison)
            || string.Equals(target, Path.GetFullPath(diffFile), comparison)
        )
            throw new ArgumentException(
                "The file output must differ from the old file and the patch file.",
                nameof(newFile)
            );
    }

    private static async Task<string> ReadMagicAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1,
            FileOptions.Asynchronous
        );
        var buffer = new byte[8];
        await stream.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
        return Encoding.ASCII.GetString(buffer);
    }

    private static string ReadMagic(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096
        );
        Span<byte> buffer = stackalloc byte[8];
        stream.ReadExactly(buffer);
        return Encoding.ASCII.GetString(buffer);
    }
}
