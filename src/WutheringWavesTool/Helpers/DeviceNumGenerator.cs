using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Haiyu.Helpers;

internal class DeviceNumGenerator
{
    private static Dictionary<string, string> localStorage = new Dictionary<string, string>();
    private const string UUID_KEY_NAME = "__KrSDK_UUID__";
    private const string CHARS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static Random random = new Random();

    public static Dictionary<string, string> ParserEnv(Dictionary<string, string> env)
    {
        var result = new Dictionary<string, string>();

        foreach (var key in env.Keys.Where(k => k.StartsWith("VITE_APP_BASEHTTPREQ")))
        {
            var newKey = key.Replace("VITE_APP_BASEHTTPREQ_", "");
            result[newKey] = env[key];
        }
        result["deviceNum"] = Uuid(32);
        result["version"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        result["sdkVersion"] = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        result["response_type"] = "code";

        return result;
    }

    /// <summary>
    /// 获取或创建UUID
    /// </summary>
    /// <param name="length">UUID长度</param>
    /// <param name="charSetLength">字符集长度</param>
    /// <returns>UUID字符串</returns>
    public static string Uuid(int length, int charSetLength = -1)
    {
        if (localStorage.ContainsKey(UUID_KEY_NAME))
        {
            return localStorage[UUID_KEY_NAME];
        }

        string newUuid = CreateUuid(length, charSetLength);

        localStorage[UUID_KEY_NAME] = newUuid;

        return newUuid;
    }
    public static string CreateUuid(int length, int charSetLength)
    {
        charSetLength = charSetLength > 0 ? charSetLength : CHARS.Length;
        var result = new char[length];

        if (length > 0)
        {
            for (int i = 0; i < length; i++)
            {
                result[i] = CHARS[random.Next(charSetLength)];
            }
        }

        return new string(result);
    }

    public static Dictionary<string, string> GetHttpBaseReq(Dictionary<string, string> env)
    {
        var baseParams = new Dictionary<string, string>
    {
        { "redirect_uri", "1" },
        { "__e__", "1" },
        { "pack_mark", "1" }
    };

        var parsedEnv = ParserEnv(env);

        // 合并基础参数和解析的环境参数
        foreach (var kvp in parsedEnv)
        {
            baseParams[kvp.Key] = kvp.Value;
        }

        return baseParams;
    }

    private static readonly Random _random = new Random();
    private static long _sequence = 0;
    private static readonly object _lock = new object();

    /// <summary>
    /// 生成类似 "198790753a2bfb-06d53a53a717f88-4c657b58-1327104-198790753a31598" 的ID
    /// </summary>
    public static string GenerateId()
    {
        string part1 = GetRandomHex(14);

        long ticks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string part2 = (ticks ^ _random.Next()).ToString("x");
        int timestamp = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string part3 = timestamp.ToString("x8");
        long seq;
        lock (_lock)
        {
            _sequence = (_sequence + 1) % 10_000_000;
            seq = _sequence;
        }
        string part4 = seq.ToString("D7");
        string part5 = GetRandomHex(15);

        return $"{part1}-{part2}-{part3}-{part4}-{part5}";
    }
    private static string GetRandomHex(int length)
    {
        byte[] buffer = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(buffer);
        }

        StringBuilder sb = new StringBuilder(length);
        foreach (byte b in buffer)
        {
            sb.Append(b.ToString("x2"));
            if (sb.Length >= length)
                break;
        }
        return sb.ToString(0, length);
    }
}


