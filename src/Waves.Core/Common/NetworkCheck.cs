namespace Waves.Core.Common;

public static class NetworkCheck
{
    public static async Task<PingReply?> PingAsync(string host, CancellationToken token)
    {
        try
        {
            var uri = new Uri(host);
            Ping ping = new();
            return await ping.SendPingAsync(uri.Host);
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public static async Task<bool> PingHostsAsync(IEnumerable<string> host,CancellationToken token = default)
    {
        foreach (var item in host)
        {
            try
            {
                var state = await PingAsync(item,token);
                if(state == null || state.Status != IPStatus.Success)
                {
                    continue;
                }
                return true;
            }
            catch (Exception)
            {
                continue;
            }
        }
        return true;
    }
}
