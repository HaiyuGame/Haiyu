using System.Security.Cryptography;

namespace Waves.Diff.Checksums;

/// <summary>
/// 按差分文件声明的 checksumType 名构造校验器。
/// 生产 krpdiff 只用 fadler64；crc32/md5 为格式合法取值故一并支持，其余直接报错。
/// </summary>
internal static class ChecksumFactory
{
    public static IChecksum Create(string checksumType) => checksumType switch
    {
        "" => new EmptyChecksum(),
        "fadler64" => new Fadler64(),
        "crc32" => new Crc32Hdiff(),
        "md5" => new Md5Checksum(),
        _ => throw new NotSupportedException(
            $"差分文件校验类型 \"{checksumType}\" 不受支持（支持：fadler64/crc32/md5）。"),
    };

    private sealed class EmptyChecksum : IChecksum
    {
        public int Size => 0;
        public void Reset() { }
        public void Update(ReadOnlySpan<byte> data) { }
        public byte[] Finish() => [];
        public void Finish(Span<byte> destination) { }
        public IChecksum New() => new EmptyChecksum();
    }

    private sealed class Md5Checksum : IChecksum
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);

        public int Size => 16;

        public void Reset()
        {
            Span<byte> discard = stackalloc byte[16];
            _hash.GetHashAndReset(discard);
        }

        public void Dispose() => _hash.Dispose();

        public void Update(ReadOnlySpan<byte> data) => _hash.AppendData(data);

        public byte[] Finish() => _hash.GetHashAndReset();
        public void Finish(Span<byte> destination) => _hash.GetHashAndReset(destination);

        public IChecksum New() => new Md5Checksum();
    }
}
