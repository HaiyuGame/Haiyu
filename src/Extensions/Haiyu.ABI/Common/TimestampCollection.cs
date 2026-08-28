// using https://github.com/DareathX/CustomFpsCounter

namespace Haiyu.ABI.Common;

public class TimestampCollection
{
    public long Id { get; }
    public string Name { get; set; }

    public TimestampCollection(long id, string name)
    {
        Id = id;
        Name = name;
    }

    private List<double> timestamps = new List<double>(1001);
    object sync = new object();

    public void Add(double timestamp)
    {
        lock (sync)
        {
            timestamps.Add(timestamp);
            if (timestamps.Count > 1000)
            {
                timestamps.RemoveAt(0);
            }
        }
    }

    public int QueryCount(double from, double to)
    {
        int count = 0;
        lock (sync)
        {
            foreach (var ts in timestamps)
            {
                if (ts >= from && ts <= to)
                {
                    count++;
                }
            }
        }
        return count;
    }
}
