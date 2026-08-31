namespace Haiyu.ABI.Common;

public sealed class TimestampCollection
{
    public long Id { get; }

    public string Name { get; set; }

    private readonly Queue<double> timestamps =
        new(1024);

    private readonly object sync =
        new();

    public TimestampCollection(
        long id,
        string name)
    {
        Id = id;
        Name = name;
    }

    public void Add(
        double timestamp)
    {
        lock (sync)
        {
            timestamps.Enqueue(
                timestamp);

            //
            // 防止长期不查询时无限增长。
            //
            while (timestamps.Count > 32768)
            {
                timestamps.Dequeue();
            }
        }
    }

    public int QueryCount(
        double from,
        double to)
    {
        lock (sync)
        {
            //
            int count = 0;

            foreach (double timestamp in timestamps)
            {
                if (timestamp > to)
                    break;

                if (timestamp >= from)
                    count++;
            }

            return count;
        }
    }

    /// <summary>取得指定时间范围的快照，不破坏较长统计窗口所需的历史数据。</summary>
    public double[] Query(double from, double to)
    {
        lock (sync)
        {
            return timestamps
                .Where(timestamp => timestamp >= from && timestamp <= to)
                .ToArray();
        }
    }

    public bool TryGetLatestTimestamp(
        out double timestamp)
    {
        lock (sync)
        {
            if (timestamps.Count == 0)
            {
                timestamp = 0;
                return false;
            }

            timestamp =
                timestamps.Last();

            return true;
        }
    }
}
