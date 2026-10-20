using System.Buffers;
using Waves.Diff.Checksums;

namespace Waves.Diff;

/// <summary>
/// HDIFF19 目录差分编排（hpatchz.exe sub_14000DF50/sub_1400059C0/sub_140004530 组合语义）：
/// 校验 4 项内嵌校验 → 打开旧引用文件 → samePair 直拷 → 内嵌 HDIFF13 重建 newRef 流并按尺寸切分写文件
/// → 逐文件校验对账 newRefHashList → 执行位 → 全流对账。
/// </summary>
internal sealed class Hdiff19Patcher
{
    private readonly string _oldRoot;
    private readonly string _diffFile;
    private readonly string _newRoot;
    private readonly string _writeRoot;
    private readonly string? _stagingRoot;
    private readonly DiffPatchOptions _options;
    private readonly Action<DiffProgress>? _onProgress;
    private readonly CancellationToken _ct;
    private readonly Hdiff19Head _head;
    private readonly IChecksum _checksumProto;
    private readonly DirData _dir;

    private Hdiff19Patcher(
        string oldRoot,
        string diffFile,
        string newRoot,
        Action<DiffProgress>? onProgress,
        CancellationToken ct,
        Hdiff19Head? head = null,
        byte[]? dirData = null,
        DiffPatchOptions? options = null
    )
    {
        _oldRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(oldRoot));
        _diffFile = diffFile;
        _newRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(newRoot));
        _options = options ?? DiffPatchOptions.Default;
        _options.Validate();
        if (PathComparer.Equals(_oldRoot, _newRoot))
        {
            string parent =
                Path.GetDirectoryName(_newRoot)
                ?? throw new ArgumentException("In-place output requires a non-volume directory.");
            _stagingRoot = Path.Combine(
                parent,
                "." + Path.GetFileName(_newRoot) + ".hdiff." + Guid.NewGuid().ToString("N")
            );
        }
        _writeRoot = _stagingRoot ?? _newRoot;
        _onProgress = onProgress;
        _ct = ct;
        _head = head ?? Hdiff19Head.Parse(diffFile);
        if (!_head.OldPathIsDir || !_head.NewPathIsDir)
            throw new NotSupportedException(
                "HDIFF19 file-root containers are not implemented by the directory API."
            );
        _checksumProto = ChecksumFactory.Create(_head.ChecksumType);
        try
        {
            if (_checksumProto.Size != (int)_head.ChecksumByteSize)
                throw new InvalidDataException(
                    $"{diffFile}：校验字节长度 {_head.ChecksumByteSize} != {_head.ChecksumType} 的 {_checksumProto.Size}。"
                );
            _dir = DirData.Parse(dirData ?? LoadDirData(), _head, diffFile);
            if (
                SumSizes(_dir.OldRefSizeList) != _head.OldRefSize
                || SumSizes(_dir.NewRefSizeList) != _head.NewRefSize
            )
                throw new InvalidDataException(
                    "Directory reference sizes differ from header totals."
                );
        }
        catch
        {
            _checksumProto.Dispose();
            throw;
        }
    }

    private static ulong SumSizes(ulong[] sizes)
    {
        ulong total = 0;
        foreach (ulong size in sizes)
        {
            if (size > long.MaxValue || size > (ulong)long.MaxValue - total)
                throw new InvalidDataException("Directory reference size overflow.");
            total += size;
        }
        return total;
    }

    public static void Patch(
        string oldRoot,
        string diffFile,
        string newRoot,
        Action<DiffProgress>? onProgress,
        CancellationToken ct,
        DiffPatchOptions? options = null
    )
    {
        ct.ThrowIfCancellationRequested();
        new Hdiff19Patcher(oldRoot, diffFile, newRoot, onProgress, ct, options: options)
            .RunAsync(false)
            .GetAwaiter()
            .GetResult();
    }

    public static async Task PatchAsync(
        string oldRoot,
        string diffFile,
        string newRoot,
        Action<DiffProgress>? onProgress,
        CancellationToken ct,
        DiffPatchOptions? options = null
    )
    {
        ct.ThrowIfCancellationRequested();
        var head = await Hdiff19Head.ParseAsync(diffFile, ct).ConfigureAwait(false);
        var data = await LoadDirDataAsync(diffFile, head, ct).ConfigureAwait(false);
        await new Hdiff19Patcher(oldRoot, diffFile, newRoot, onProgress, ct, head, data, options)
            .RunAsync(true)
            .ConfigureAwait(false);
    }

    private const int BufferSize = 128 * 1024;
    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static FileStream OpenRead(string path, bool asyncIo) =>
        new(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 1,
                Options =
                    FileOptions.SequentialScan
                    | (asyncIo ? FileOptions.Asynchronous : FileOptions.None),
            }
        );

    private static FileStream OpenWrite(string path, bool asyncIo) =>
        new(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 64 * 1024,
                Options =
                    FileOptions.SequentialScan
                    | (asyncIo ? FileOptions.Asynchronous : FileOptions.None),
            }
        );

    private static string TemporaryPath(string path) =>
        path + ".hdiff." + Guid.NewGuid().ToString("N") + ".tmp";

    private byte[] LoadDirData()
    {
        _ct.ThrowIfCancellationRequested();
        using var fs = OpenRead(_diffFile, false);
        using var region = new RegionStream(fs, _head.DirDataStart, _head.DirDataFileSize);
        var raw = new byte[checked((int)_head.DirDataRawSize)];
        if (_head.DirDataCompSize == 0)
        {
            region.ReadExactly(raw);
            return raw;
        }
        // Decompress directly from the bounded file region: no compressed metadata copy.
        using var ds = new ZstdNet.DecompressionStream(region);
        int done = 0;
        while (done < raw.Length)
        {
            _ct.ThrowIfCancellationRequested();
            int n = ds.Read(raw, done, Math.Min(BufferSize, raw.Length - done));
            if (n == 0)
                throw new InvalidDataException($"{_diffFile}：dirData 解压长度与声明不符。");
            done += n;
        }
        if (ds.ReadByte() != -1)
            throw new InvalidDataException($"{_diffFile}：dirData 解压长度与声明不符。");
        return raw;
    }

    private static async Task<byte[]> LoadDirDataAsync(
        string path,
        Hdiff19Head head,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        await using var file = OpenRead(path, true);
        using var region = new RegionStream(file, head.DirDataStart, head.DirDataFileSize);
        using var decoded =
            head.DirDataCompSize == 0 ? (Stream)region : new ZstdNet.DecompressionStream(region);
        var raw = new byte[checked((int)head.DirDataRawSize)];
        for (int offset = 0; offset < raw.Length; )
        {
            int count = Math.Min(BufferSize, raw.Length - offset);
            await decoded.ReadExactlyAsync(raw.AsMemory(offset, count), ct).ConfigureAwait(false);
            offset += count;
        }
        var tail = new byte[1];
        if (await decoded.ReadAsync(tail, ct).ConfigureAwait(false) != 0)
            throw new InvalidDataException($"{path}：dirData 解压长度与声明不符。");
        return raw;
    }

    private async Task RunAsync(bool asyncIo)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var pendingRenames = new List<(string Temp, string Final)>();
        try
        {
            if (ShouldValidate(DiffChecksumValidation.Diff))
                await VerifyDiffSelfChecksumAsync(buffer, asyncIo).ConfigureAwait(false);
            var oldRefPaths = ResolveOldRefPaths();
            if (ShouldValidate(DiffChecksumValidation.Old))
                await VerifyOldRefChecksumAsync(oldRefPaths, buffer, asyncIo).ConfigureAwait(false);
            SingleDiffDispatcher.Info singleInfo;
            using (var headerStream = OpenRead(_diffFile, asyncIo))
                singleInfo = asyncIo
                    ? await SingleDiffDispatcher
                        .InspectAsync(headerStream, _head.SingleDiffStart, _head.SingleDiffEnd, _ct)
                        .ConfigureAwait(false)
                    : SingleDiffDispatcher.Inspect(
                        headerStream,
                        _head.SingleDiffStart,
                        _head.SingleDiffEnd
                    );
            if (
                singleInfo.OldSize != _head.OldRefSize
                || singleInfo.NewSize != _head.NewRefSize
                || (
                    singleInfo.Compression.Length != 0
                    && singleInfo.Compression != _head.CompressType
                )
            )
                throw new InvalidDataException(
                    "Nested diff sizes or compression differ from the directory header."
                );
            var unchangedOutputs = new bool[_dir.NewPaths.Length];

            var refSpecs = new (string Path, ulong Size, ulong Hash)[_dir.NewRefList.Length];
            for (int i = 0; i < refSpecs.Length; i++)
                refSpecs[i] = (
                    Path.Combine(_writeRoot, _dir.NewPaths[(int)_dir.NewRefList[i]]),
                    _dir.NewRefSizeList[i],
                    _dir.NewRefHashList[i]
                );

            // Indexed metadata avoids retaining a second set of all path strings.
            var fileNodes = new bool[_dir.NewPaths.Length];
            foreach (var idx in _dir.NewRefList)
                fileNodes[(int)idx] = true;
            foreach (var (ni, _) in _dir.SamePairs)
            {
                if (fileNodes[(int)ni])
                    throw new InvalidDataException(
                        "Duplicate output index in newRef and samePair."
                    );
                fileNodes[(int)ni] = true;
            }
            var tracker = new ProgressTracker(
                (ulong)(_dir.NewRefList.Length + _dir.SamePairs.Length),
                _head.NewRefSize + _head.CopySize,
                _onProgress
            );
            // Preserve every old source, including samePair sources that a later copy needs.
            var oldRefSet = new HashSet<string>(oldRefPaths, PathComparer);
            foreach (var (_, oi) in _dir.SamePairs)
                oldRefSet.Add(Path.Combine(_oldRoot, _dir.OldPaths[(int)oi]));

            Directory.CreateDirectory(_writeRoot);
            for (int i = 0; i < _dir.NewPaths.Length; i++)
            {
                _ct.ThrowIfCancellationRequested();
                string relative = _dir.NewPaths[i];
                string full = Path.Combine(_writeRoot, relative);
                bool isDirectory =
                    relative.EndsWith(Path.DirectorySeparatorChar) || relative.Length == 0;
                if (fileNodes[i])
                {
                    if (isDirectory)
                        throw new InvalidDataException("File index refers to a directory path.");
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                }
                else if (isDirectory)
                    Directory.CreateDirectory(full);
                else
                {
                    // sub_140005090: unreferenced paths without a trailing slash are empty files.
                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    string writePath = oldRefSet.Contains(full) ? TemporaryPath(full) : full;
                    if (writePath != full)
                        pendingRenames.Add((writePath, full));
                    using var empty = OpenWrite(writePath, asyncIo);
                }
            }

            using var copyChecksum = _checksumProto.New();
            ulong copiedBytes = 0;
            foreach (var (ni, oi) in _dir.SamePairs)
            {
                _ct.ThrowIfCancellationRequested();
                string src = Path.Combine(_oldRoot, _dir.OldPaths[(int)oi]);
                string dst = Path.Combine(_writeRoot, _dir.NewPaths[(int)ni]);
                using var input = OpenRead(src, asyncIo);
                long remain = input.Length;
                tracker.BeginFile((ulong)remain);
                string? writePath = null;
                FileStream? output = null;
                try
                {
                    bool unchanged = PathComparer.Equals(
                        src,
                        Path.Combine(_newRoot, _dir.NewPaths[(int)ni])
                    );
                    unchangedOutputs[(int)ni] = unchanged;
                    if (!unchanged)
                    {
                        writePath = oldRefSet.Contains(dst) ? TemporaryPath(dst) : dst;
                        if (writePath != dst)
                            pendingRenames.Add((writePath, dst));
                        output = OpenWrite(writePath, asyncIo);
                    }
                    // Even unchanged in-place files must contribute to checksum[2].
                    while (remain > 0)
                    {
                        _ct.ThrowIfCancellationRequested();
                        int n = (int)Math.Min(buffer.Length, remain);
                        if (asyncIo)
                            await input
                                .ReadExactlyAsync(buffer.AsMemory(0, n), _ct)
                                .ConfigureAwait(false);
                        else
                            input.ReadExactly(buffer.AsSpan(0, n));
                        if (ShouldValidate(DiffChecksumValidation.Copy))
                            copyChecksum.Update(buffer.AsSpan(0, n));
                        if (output != null)
                        {
                            if (asyncIo)
                                await output
                                    .WriteAsync(buffer.AsMemory(0, n), _ct)
                                    .ConfigureAwait(false);
                            else
                                output.Write(buffer, 0, n);
                        }
                        remain -= n;
                        copiedBytes = checked(copiedBytes + (ulong)n);
                        tracker.Chunk((ulong)n);
                    }
                }
                finally
                {
                    if (output != null)
                    {
                        if (asyncIo)
                            await output.DisposeAsync().ConfigureAwait(false);
                        else
                            output.Dispose();
                    }
                }
                tracker.EndFile();
            }
            if (copiedBytes != _head.CopySize)
                throw new InvalidDataException($"{_diffFile}：samePair 总长度与声明不符。");
            if (ShouldValidate(DiffChecksumValidation.Copy))
                VerifyChecksum(
                    copyChecksum.Finish(),
                    _head.Checksums[2],
                    "copyData(samePair) 校验失败"
                );

            using (
                var oldStream = new ConcatFileStream(
                    oldRefPaths,
                    _dir.OldRefSizeList.Select(s => checked((long)s)).ToArray(),
                    asyncIo
                )
            )
            using (var diffStream = OpenRead(_diffFile, asyncIo))
            {
                using var newWriter = new MultiFileStreamWriter(
                    refSpecs,
                    _checksumProto.New(),
                    tracker,
                    _diffFile,
                    oldRefSet,
                    pendingRenames,
                    asyncIo,
                    _ct,
                    (_options.Checksums & DiffChecksumValidation.New) != 0,
                    ShouldValidate(DiffChecksumValidation.New)
                );
                if (asyncIo)
                    await SingleDiffDispatcher
                        .PatchAsync(
                            singleInfo,
                            diffStream,
                            _diffFile,
                            _head.SingleDiffStart,
                            _head.SingleDiffEnd,
                            oldStream,
                            newWriter,
                            null,
                            _ct
                        )
                        .ConfigureAwait(false);
                else
                    SingleDiffDispatcher.Patch(
                        singleInfo,
                        diffStream,
                        _diffFile,
                        _head.SingleDiffStart,
                        _head.SingleDiffEnd,
                        oldStream,
                        newWriter,
                        null,
                        _ct
                    );
                newWriter.Finish();
                if (ShouldValidate(DiffChecksumValidation.New))
                    VerifyChecksum(
                        newWriter.AggregateChecksum(),
                        _head.Checksums[1],
                        "newRefData 校验失败"
                    );
            }

            _ct.ThrowIfCancellationRequested();
            tracker.Committing();
            _ct.ThrowIfCancellationRequested();
            foreach (var (temp, final) in pendingRenames)
                File.Move(temp, final, true);
            CommitStagedOutput(unchangedOutputs);
            ApplyExecuteBits();
            tracker.Complete();
        }
        finally
        {
            _checksumProto.Dispose();
            ArrayPool<byte>.Shared.Return(buffer);
            CleanupStaging();
            foreach (var (temp, _) in pendingRenames)
            {
                try
                {
                    File.Delete(temp);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private bool ShouldValidate(DiffChecksumValidation flag) =>
        _checksumProto.Size != 0 && (_options.Checksums & flag) != 0;

    // Publish only paths produced by this patch. OldPaths/SrcFiles cleanup belongs to Haiyu.
    // Once publication starts it is non-cancelable and is not a multi-file atomic transaction.
    private void CommitStagedOutput(bool[] unchangedOutputs)
    {
        if (_stagingRoot == null)
            return;
        for (int i = 0; i < _dir.NewPaths.Length; i++)
        {
            string relative = _dir.NewPaths[i];
            string target = Path.Combine(_newRoot, relative);
            if (relative.Length == 0 || relative.EndsWith(Path.DirectorySeparatorChar))
                Directory.CreateDirectory(target);
            else if (!unchangedOutputs[i])
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(Path.Combine(_stagingRoot, relative), target, true);
            }
        }
    }

    private void CleanupStaging()
    {
        if (_stagingRoot == null)
            return;
        // The absolute, randomly named sibling is the only directory this operation owns.
        if (
            !PathComparer.Equals(
                Path.GetDirectoryName(_stagingRoot),
                Path.GetDirectoryName(_newRoot)
            )
            || !_stagingRoot.StartsWith(
                Path.Combine(
                    Path.GetDirectoryName(_newRoot)!,
                    "." + Path.GetFileName(_newRoot) + ".hdiff."
                ),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal
            )
        )
            throw new InvalidOperationException("Unexpected staging directory.");
        try
        {
            if (Directory.Exists(_stagingRoot))
                Directory.Delete(_stagingRoot, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private string[] ResolveOldRefPaths()
    {
        var paths = new string[_dir.OldRefList.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            _ct.ThrowIfCancellationRequested();
            string p = Path.Combine(_oldRoot, _dir.OldPaths[(int)_dir.OldRefList[i]]);
            var info = new FileInfo(p);
            if (!info.Exists)
                throw new FileNotFoundException($"{_diffFile}：旧引用文件缺失：{p}");
            if (info.Length != (long)_dir.OldRefSizeList[i])
                throw new InvalidDataException(
                    $"{_diffFile}：旧引用文件 {p} 大小 {info.Length} != 差分声明 {_dir.OldRefSizeList[i]}。"
                );
            paths[i] = p;
        }
        return paths;
    }

    /// <summary>checksum[3]：整个差分文件去掉 checksum[3] 自身槽位的校验（sub_140003480 语义）。</summary>
    private async ValueTask VerifyDiffSelfChecksumAsync(byte[] buffer, bool asyncIo)
    {
        using var cs = _checksumProto.New();
        int size = (int)_head.ChecksumByteSize;
        long skipStart = _head.ChecksumAreaStart + 3L * size;
        long skipEnd = _head.ChecksumAreaStart + 4L * size;
        using var fs = OpenRead(_diffFile, asyncIo);
        for (int region = 0; region < 2; region++)
        {
            fs.Position = region == 0 ? 0 : skipEnd;
            long remain = region == 0 ? skipStart : fs.Length - skipEnd;
            while (remain > 0)
            {
                _ct.ThrowIfCancellationRequested();
                int n = (int)Math.Min(buffer.Length, remain);
                if (asyncIo)
                    await fs.ReadExactlyAsync(buffer.AsMemory(0, n), _ct).ConfigureAwait(false);
                else
                    fs.ReadExactly(buffer.AsSpan(0, n));
                cs.Update(buffer.AsSpan(0, n));
                remain -= n;
            }
        }
        VerifyChecksum(cs.Finish(), _head.Checksums[3], "差分文件自校验失败");
    }

    /// <summary>checksum[0]：旧引用数据拼接流整体校验。</summary>
    private async ValueTask VerifyOldRefChecksumAsync(
        string[] oldRefPaths,
        byte[] buffer,
        bool asyncIo
    )
    {
        using var cs = _checksumProto.New();
        foreach (var p in oldRefPaths)
        {
            _ct.ThrowIfCancellationRequested();
            using var fs = OpenRead(p, asyncIo);
            while (true)
            {
                _ct.ThrowIfCancellationRequested();
                int n = asyncIo
                    ? await fs.ReadAsync(buffer.AsMemory(), _ct).ConfigureAwait(false)
                    : fs.Read(buffer, 0, buffer.Length);
                if (n == 0)
                    break;
                cs.Update(buffer.AsSpan(0, n));
            }
        }
        VerifyChecksum(cs.Finish(), _head.Checksums[0], "oldRefData(旧文件) 校验失败");
    }

    private void ApplyExecuteBits()
    {
        if (!OperatingSystem.IsWindows())
        {
            foreach (var idx in _dir.NewExecuteList)
                File.SetUnixFileMode(
                    Path.Combine(_newRoot, _dir.NewPaths[(int)idx]),
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                );
        }
        // Windows 无执行位，newExecuteList 为 no-op（与 hpatchz Windows 行为一致）
    }

    private void VerifyChecksum(byte[] actual, byte[] expected, string what)
    {
        if (!actual.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException(
                $"{_diffFile}：{what}（实际 {Convert.ToHexString(actual)} != 期望 {Convert.ToHexString(expected)}）。"
            );
    }

    /// <summary>按 newRefSizeList 切分写出 newRef 流；写满即逐文件对账 newRefHashList。
    /// 目标命中旧引用集合时写临时路径，由调用方在旧句柄释放后统一 rename 就位。</summary>
    private sealed class MultiFileStreamWriter : Stream
    {
        private readonly (string Path, ulong Size, ulong Hash)[] _specs;
        private readonly IChecksum _global;
        private readonly IChecksum _perFile;
        private readonly bool _validateFiles;
        private readonly bool _validateGlobal;
        private readonly bool _combineFadler;
        private readonly ProgressTracker _tracker;
        private readonly string _diffName;
        private readonly HashSet<string> _oldRefSet;
        private readonly List<(string Temp, string Final)> _pendingRenames;
        private FileStream? _current;
        private int _index;
        private long _writtenInFile;
        private readonly bool _asyncIo;
        private readonly CancellationToken _ct;

        public MultiFileStreamWriter(
            (string Path, ulong Size, ulong Hash)[] specs,
            IChecksum checksum,
            ProgressTracker tracker,
            string diffName,
            HashSet<string> oldRefSet,
            List<(string Temp, string Final)> pendingRenames,
            bool asyncIo,
            CancellationToken ct,
            bool validateFiles,
            bool validateGlobal
        )
        {
            _validateFiles = validateFiles;
            _validateGlobal = validateGlobal;
            _specs = specs;
            _asyncIo = asyncIo;
            _ct = ct;
            _global = checksum;
            // Native sub_1400055E0 updates the per-file state via sub_14000FE90 (fadler64),
            // separately from the selected global checksum plugin; sub_140005460 compares this ulong.
            _perFile = new Fadler64();
            _combineFadler = validateFiles && validateGlobal && checksum is Fadler64;
            _tracker = tracker;
            _diffName = diffName;
            _oldRefSet = oldRefSet;
            _pendingRenames = pendingRenames;
        }

        public byte[] AggregateChecksum() => _global.Finish();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        private void OpenCurrentFile()
        {
            if (_index >= _specs.Length)
                throw new InvalidDataException($"{_diffName}：newRef 流超出 newRefSizeList 总量。");
            var (path, size, _) = _specs[_index];
            if (size > long.MaxValue)
                throw new InvalidDataException($"{_diffName}：newRef 文件尺寸越界。");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string openPath = path;
            if (_oldRefSet.Contains(path))
            {
                openPath = TemporaryPath(path);
                _pendingRenames.Add((openPath, path));
            }
            _current = OpenWrite(openPath, _asyncIo);
            _perFile.Reset();
            _writtenInFile = 0;
            _tracker.BeginFile(size);
        }

        private void FinishEmptyFiles()
        {
            while (_current == null && _index < _specs.Length && _specs[_index].Size == 0)
            {
                _ct.ThrowIfCancellationRequested();
                OpenCurrentFile();
                CloseCurrentFile();
            }
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            while (!buffer.IsEmpty)
            {
                _ct.ThrowIfCancellationRequested();
                FinishEmptyFiles();
                if (_current == null)
                    OpenCurrentFile();
                int n = (int)Math.Min(buffer.Length, (long)_specs[_index].Size - _writtenInFile);
                _current!.Write(buffer[..n]);
                AccountWrite(buffer[..n]);
                buffer = buffer[n..];
            }
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            while (!buffer.IsEmpty)
            {
                _ct.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                FinishEmptyFiles();
                if (_current == null)
                    OpenCurrentFile();
                int n = (int)Math.Min(buffer.Length, (long)_specs[_index].Size - _writtenInFile);
                await _current!.WriteAsync(buffer[..n], cancellationToken).ConfigureAwait(false);
                AccountWrite(buffer.Span[..n]);
                buffer = buffer[n..];
            }
        }

        private void AccountWrite(ReadOnlySpan<byte> data)
        {
            if (_validateFiles)
                _perFile.Update(data);
            if (_validateGlobal && !_combineFadler)
                _global.Update(data);
            _writtenInFile += data.Length;
            _tracker.Chunk((ulong)data.Length);
            if (_writtenInFile == (long)_specs[_index].Size)
                CloseCurrentFile();
        }

        private void CloseCurrentFile()
        {
            _current!.Dispose();
            _current = null;
            var expected = _specs[_index].Hash;
            // 逐文件校验值按小端 ulong 存储于 newRefHashList
            Span<byte> hash = stackalloc byte[8];
            if (_validateFiles) _perFile.Finish(hash);
            if (_validateFiles && System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(hash) != expected)
                throw new InvalidDataException(
                    $"{_diffName}：新引用文件 {_specs[_index].Path} 校验失败"
                        + $"（实际 {Convert.ToHexString(hash)} != 期望 0x{expected:X16}）。"
                );
            // Production patches use Fadler64 for both per-file and aggregate checks.
            // Combine the validated file state instead of hashing all output twice.
            if (_combineFadler)
                ((Fadler64)_global).Append((Fadler64)_perFile, (ulong)_writtenInFile);
            _index++;
            _tracker.EndFile();
        }

        public void Finish()
        {
            FinishEmptyFiles();
            if (_current != null || _index != _specs.Length)
                throw new InvalidDataException(
                    $"{_diffName}：newRef 流未写满 newRefSizeList 总量。"
                );
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _current?.Dispose();
                _global.Dispose();
                _perFile.Dispose();
            }
            base.Dispose(disposing);
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>六字段进度聚合与节流上报（对齐 hpatchz 共享内存写入时机）。</summary>
    private sealed class ProgressTracker
    {
        private readonly Action<DiffProgress>? _onProgress;
        private readonly ulong _fileTotal;
        private readonly ulong _totalBytes;
        private ulong _patchedFiles;
        private ulong _fileSize;
        private ulong _fileDone;
        private ulong _globalDone;
        private ulong _sinceLastReport;
        private DiffPatchStage _stage;
        private readonly DiffSpeedMeter? _speed;
        private const ulong ReportThreshold = 1 << 20;

        public ProgressTracker(ulong fileTotal, ulong totalBytes, Action<DiffProgress>? onProgress)
        {
            _fileTotal = fileTotal;
            _totalBytes = totalBytes;
            _onProgress = onProgress;
            _speed = onProgress is null ? null : new DiffSpeedMeter();
        }

        public void BeginFile(ulong size)
        {
            _fileSize = size;
            _fileDone = 0;
            Report();
        }

        public void Chunk(ulong bytes)
        {
            _fileDone += bytes;
            _globalDone += bytes;
            _sinceLastReport += bytes;
            if (_sinceLastReport >= ReportThreshold)
                Report();
        }

        public void EndFile()
        {
            _patchedFiles++;
            Report();
        }

        public void Committing()
        {
            _stage = DiffPatchStage.Committing;
            Report();
        }

        public void Complete()
        {
            _stage = DiffPatchStage.Completed;
            Report();
        }

        private void Report()
        {
            _sinceLastReport = 0;
            _onProgress?.Invoke(
                new DiffProgress
                {
                    Stage = _stage,
                    PatchedFileCount = _patchedFiles,
                    FileTotalCount = _fileTotal,
                    PatchingFileCurrentSize = _fileDone,
                    PatchingFileTotalSize = _fileSize,
                    PatchedCurrentBytes = _globalDone,
                    PatchTotalBytes = _totalBytes,
                    SpeedValue = _speed?.Sample(
                        _globalDone,
                        _stage == DiffPatchStage.Completed
                    ) ?? 0,
                }
            );
        }
    }
}
