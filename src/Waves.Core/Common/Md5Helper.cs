namespace Waves.Core.Common;

public static class Md5Helper
{
    public static string ComputeMd532(string input, Encoding encoding = null, bool isUpper = false)
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentNullException(nameof(input));
        encoding ??= Encoding.UTF8;
        byte[] inputBytes = encoding.GetBytes(input);
        using (MD5 md5 = MD5.Create())
        {
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString(isUpper ? "X2" : "x2"));
            }
            return sb.ToString();
        }
    }
}
