using System.Text;
using System.Text.RegularExpressions;

namespace Waves.Core.INI;

/// <summary>
/// Ini 读写类
/// </summary>
public class Ini
{
    private Dictionary<string, List<Tuple<string, string>>> _inis = new Dictionary<string, List<Tuple<string, string>>>();

    public string FilePath { get; private set; }
    public Encoding Encoding { get; private set; }

    private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

    public async Task FromFileAsync(string path, Encoding encoding, CancellationToken token = default)
    {
        try
        {
            this.FilePath = path;
            this.Encoding = encoding;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true))
            {
                var dict = await ToDictionaryAsync(fs, encoding, token).ConfigureAwait(false);
                _rwLock.EnterWriteLock();
                try
                {
                    _inis = dict;
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task FromStreamAsync(Stream stream, Encoding encoding, CancellationToken token = default)
    {
        try
        {
            var dict = await ToDictionaryAsync(stream, encoding, token).ConfigureAwait(false);
            _rwLock.EnterReadLock();
            try
            {
                _inis = dict;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
        catch(Exception ex)
        {
            throw ex;
        }
    }

    private async Task<Dictionary<string, List<Tuple<string, string>>>> ToDictionaryAsync(Stream stream, Encoding encoding, CancellationToken token = default)
    {
        var keyPairs = new Dictionary<string, List<Tuple<string, string>>>();
        using (var reader = new StreamReader(stream, encoding: encoding))
        {
            string currentSection = string.Empty;
            while (!reader.EndOfStream)
            {
                token.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                    break;
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line.StartsWith(";") || line.StartsWith("#"))
                    continue;
                if (line.StartsWith("["))
                {
                    var match = Regex.Match(line, @"\[(?<inner>[^\[\]]+)\]");
                    if (!match.Success)
                        throw new Exception("文件损坏！");
                    var sectionName = match.Groups["inner"].Value.Trim();
                    if (keyPairs.ContainsKey(sectionName))
                        throw new Exception("重复的Key");
                    keyPairs.Add(sectionName, new List<Tuple<string, string>>());
                    currentSection = sectionName;
                }
                else
                {
                    if (string.IsNullOrEmpty(currentSection))
                        throw new Exception("文件格式错误：在节之前存在键值对");
                    var idx = line.IndexOf('=');
                    if (idx < 0)
                    {
                        var prop = line.Trim();
                        keyPairs[currentSection].Add(new Tuple<string, string>(prop, string.Empty));
                    }
                    else
                    {
                        var property = line.Substring(0, idx).Trim();
                        var value = line.Substring(idx + 1).Trim();
                        keyPairs[currentSection].Add(new Tuple<string, string>(property, value));
                    }
                }
            }
        }
        return keyPairs;
    }

    public async Task<string> GetValueAsync(string section, string key)
    {
        _rwLock.EnterReadLock();
        try
        {
            if (!_inis.ContainsKey(section))
                return null;
            var sectionList = _inis[section];
            foreach (var item in sectionList)
            {
                if (item.Item1 == key)
                    return item.Item2;
            }
            return null;
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public async Task SetValueAsync(string section, string key, string value)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (!_inis.ContainsKey(section))
            {
                _inis[section] = new List<Tuple<string, string>>();
            }
            var list = _inis[section];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Item1 == key)
                {
                    list[i] = new Tuple<string, string>(key, value);
                    return;
                }
            }
            list.Add(new Tuple<string, string>(key, value));
            if (string.IsNullOrWhiteSpace(FilePath))
                return;
            _rwLock.ExitWriteLock();
            try
            {
                await SaveToFileAsync(FilePath, Encoding).ConfigureAwait(false);
            }
            finally
            {
                _rwLock.EnterWriteLock();
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public async Task SaveToFileAsync(string path, Encoding encoding, CancellationToken token = default)
    {
        var dir = Path.GetDirectoryName(path) ?? string.Empty;
        string content;
        _rwLock.EnterReadLock();
        try
        {
            content = ToIniString();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
        if (File.Exists(path))
            File.Delete(path);
        using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
        using (var writer = new StreamWriter(fs, encoding))
        {
            await writer.WriteAsync(content.AsMemory(), token).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
    }

    private string ToIniString()
    {
        var sb = new StringBuilder();
        foreach (var section in _inis)
        {
            sb.Append('[').Append(section.Key).Append(']').AppendLine();
            foreach (var kv in section.Value)
            {
                sb.Append(kv.Item1).Append('=').Append(kv.Item2 ?? string.Empty).AppendLine();
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
