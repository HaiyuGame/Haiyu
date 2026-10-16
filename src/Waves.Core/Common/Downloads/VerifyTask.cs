namespace Waves.Core.Common.Downloads;

public static class VerifyTask
{
    const int MaxBufferSize = 262144;

    const long UpdateThreshold = 1048576;

    public static Task<bool> ValidateGameFileAsync(string hash, string filePath, DownloadState state,
        CancellationTokenSource downloadCts,
        IProgress<(GameContextActionType, bool, long, string, long, long)>? progress = null) =>
        ValidateGameRangeAsync(hash, filePath, 0, null, state, downloadCts, progress);

    public static Task<bool> ValidateFileChunks(GameFileChunkInfo chunk, string filePath,
        DownloadState state = null, CancellationTokenSource? downloadCts = default,
        IProgress<(GameContextActionType, bool, long, string, long, long)>? progress = null) =>
        ValidateGameRangeAsync(chunk.Hash, filePath, chunk.Start, chunk.End, state, downloadCts!, progress);

    private static async Task<bool> ValidateGameRangeAsync(string hash, string path, long start, long? end,
        DownloadState? state, CancellationTokenSource cts,
        IProgress<(GameContextActionType, bool, long, string, long, long)>? progress)
    {
        cts.Token.ThrowIfCancellationRequested();
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, MaxBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (end.HasValue && (end < start || stream.Length <= end.Value)) return true;
            stream.Position = start;
            var remaining = end.HasValue ? end.Value - start + 1 : stream.Length;
            var total = remaining;
            using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            var buffer = ArrayPool<byte>.Shared.Rent(MaxBufferSize);
            try
            {
                long accumulatedBytes = 0;
                while (remaining > 0)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    if (state is not null) await state.PauseToken.WaitIfPausedAsync().ConfigureAwait(false);
                    var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(remaining, MaxBufferSize)), cts.Token).ConfigureAwait(false);
                    if (read == 0) return true;
                    md5.AppendData(buffer, 0, read);
                    remaining -= read;
                    accumulatedBytes += read;
                    if (accumulatedBytes >= UpdateThreshold)
                    {
                        progress?.Report((GameContextActionType.Verify, false, accumulatedBytes, path, total - remaining, total));
                        accumulatedBytes = 0;
                    }
                }
                var mismatch = !string.Equals(Convert.ToHexString(md5.GetHashAndReset()), hash, StringComparison.OrdinalIgnoreCase);
                if (accumulatedBytes > 0)
                    progress?.Report((GameContextActionType.Verify, false, accumulatedBytes, path, total, total));
                return mismatch;
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

}
