using System.Collections;
using ABI.Models;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;

namespace Haiyu.ABI.Common;

public class MonitorCounter
{
    Computer _computer;
    System.Threading.SemaphoreSlim _semaphoreSlim = new(1, 1);
    System.Threading.PeriodicTimer _timer;

    public MonitorCounter()
    {
        _computer = new Computer()
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true
        };
    }

    public void Start()
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _computer.Open();
        _ = Task.Run(Monitor);
    }

    private async Task Monitor()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            try
            {
                await _semaphoreSlim.WaitAsync();

                foreach (var hardware in _computer.Hardware)
                    UpdateRecursive(hardware);
                foreach (var printHardwawre in _computer.Hardware)
                {
                    if(printHardwawre.HardwareType == HardwareType.Cpu)
                    {
                        GetCPUData(printHardwawre);
                    }
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }
    }

    private CPUData GetCPUData(IHardware printHardwawre)
    {
        CPUData data = new();
        var sensor =  FindBestSensor(printHardwawre, SensorType.Load, ["CPU Total"]);
        return data;
    }

    ISensor? FindBestSensor(
    IHardware hardware,
    SensorType type,
    string[] preferredNames)
    {
        var sensors = hardware.Sensors
            .Where(x =>
                x.SensorType == type &&
                x.Value.HasValue)
            .ToArray();

        if (sensors.Length == 0)
            return null;

        foreach (var preferred in preferredNames)
        {
            var exact = sensors.FirstOrDefault(x =>
                x.Name.Equals(
                    preferred,
                    StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
                return exact;
        }

        foreach (var preferred in preferredNames)
        {
            var contains = sensors.FirstOrDefault(x =>
                x.Name.Contains(
                    preferred,
                    StringComparison.OrdinalIgnoreCase));

            if (contains is not null)
                return contains;
        }

        // 尽量别选完全静止的 0 值传感器
        var nonZero = sensors.FirstOrDefault(x =>
            Math.Abs(x.Value!.Value) > 0.0001f);

        return nonZero ?? sensors[0];
    }

    /// <summary>
    /// 更新计数器
    /// </summary>
    /// <param name="hardware"></param>
    private void UpdateRecursive(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
            UpdateRecursive(subHardware);
    }
}
