namespace Waves.Core.Common;

public static class KrKeyHelper
{
    /// <summary>
    /// 异或加密，key99
    /// </summary>
    /// <param name="data"></param>
    /// <param name="xorKey"></param>
    /// <returns></returns>
    public static byte[]? Xor(byte[] data, byte xorKey)
    {
        if (data == null)
        {
            return null;
        }
        if (data.Length == 0)
        {
            return data;
        }
        byte[] array = new byte[data.Length];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = (byte)(data[i] ^ xorKey);
        }
        return array;
    }

    /// <summary>
    /// 网页密钥异或加密
    /// </summary>
    /// <param name="data"></param>
    /// <param name="xorKey"></param>
    /// <returns></returns>
    public static string Xor(string data, int xorKey)
    {
        StringBuilder decryptedCode = new StringBuilder();

        foreach (char c in data)
        {
            char decryptedChar = (char)((int)c ^ xorKey);
            decryptedCode.Append(decryptedChar);
        }

        return decryptedCode.ToString();
    }

    /// <summary>
    /// 鸣潮 ELog 日志加密的双字节 key：
    /// 写入时按明文字节奇偶选择 key 异或；两个 key 均为奇数，XOR 必然翻转字节奇偶，
    /// 因此解密时按密文字节奇偶反查即可与加密互逆。
    /// </summary>
    private const byte ELogKeyEven = 0xA5; // 明文偶数字节使用的 key
    private const byte ELogKeyOdd = 0xEF;  // 明文奇数字节使用的 key

    /// <summary>
    /// 判断 Client*.log 内容是否为 ELog 加密状态：
    /// 明文日志以 UTF-8 BOM（EF BB BF）开头；加密内容的字节奇偶与明文相反，
    /// BOM 三个字节的奇偶均会被翻转，故加密内容不以 BOM 开头。
    /// 该判定对任意 key 组合（两个 key 均为奇数）都成立，key 变化不影响判定。
    /// </summary>
    /// <param name="data">日志文件头部字节（至少 3 字节）</param>
    public static bool IsELogEncrypted(ReadOnlySpan<byte> data) =>
        data.Length >= 3 && !(data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF);

    /// <summary>
    /// 解密（或加密，XOR 自逆）鸣潮 ELog 日志内容：
    /// 密文偶数字节（含 0x00）对应明文奇数字节，使用 0xEF；密文奇数字节使用 0xA5。
    /// 对加密输入即解密、对明文输入即加密，与游戏读回日志的转换一致。
    /// </summary>
    /// <remarks>
    /// key 变更后的定位方式：
    /// 加密函数的代码特征码（Operand 通配）为
    /// <c>48 85 D2 0F 84 ? ? ? ? 55 56 41 56 48 83 EC 20 49 8B E8</c>，
    /// 在新版本二进制中用该特征码定位加密函数，函数体内形如
    /// <c>xor al, byte_xxx[reg &amp; 1]</c> 的查表异或所引用的两个常量即为新 key：
    /// 表首项作用于明文偶数字节，第二项作用于明文奇数字节。
    /// </remarks>
    public static byte[] TransformELog(ReadOnlySpan<byte> data)
    {
        var result = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            result[i] = (byte)(b ^ ((b & 1) == 0 ? ELogKeyOdd : ELogKeyEven));
        }
        return result;
    }
}
