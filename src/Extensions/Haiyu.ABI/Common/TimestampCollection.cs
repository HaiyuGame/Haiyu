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
            while (timestamps.Count > 8192)
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
            // 已经过期的历史数据可以直接扔掉。
            //
            while (
                timestamps.TryPeek(
                    out double timestamp) &&
                timestamp < from)
            {
                timestamps.Dequeue();
            }

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
