using System.Runtime.InteropServices;
using ABI.Models;
using ABIRuntime.Abstractions;

namespace Haiyu.ABI.Services;

/// <summary>独立于按键/鼠标映射的只读快照源，不创建后台轮询任务。</summary>
public interface IXboxSessionSource
{
    XboxSession[] GetSessions();
}

/// <summary>直接调用系统 XInput 1.4，不依赖 Windows SDK/WinRT。</summary>
public sealed partial class XInputSessionSource : IXboxSessionSource
{
    public XboxSession[] GetSessions()
    {
        var sessions = new XboxSession[4];
        for (uint index = 0; index < sessions.Length; index++)
        {
            uint result = XInputGetState(index, out _);
            if (result == 1167) // ERROR_DEVICE_NOT_CONNECTED
            {
                sessions[index] = new XboxSession
                {
                    UserIndex = (int)index,
                    BatteryType = XboxBatteryType.Disconnected,
                };
                continue;
            }
            if (result != 0)
            {
                sessions[index] = new XboxSession
                {
                    UserIndex = (int)index,
                    StateErrorCode = result,
                };
                continue;
            }

            var session = new XboxSession { UserIndex = (int)index, IsConnected = true };
            // Capability/battery failures leave the corresponding information Unknown.
            if (XInputGetCapabilities(index, 0, out var capabilities) == 0)
                session.ConnectionType =
                    (capabilities.Flags & 0x0002) != 0
                        ? XboxConnectionType.Wireless
                        : XboxConnectionType.Wired;
            if (XInputGetBatteryInformation(index, 0, out var battery) == 0)
                ApplyBattery(session, battery.Type, battery.Level);

            // An unplug between the state and metadata queries must not retain stale battery data.
            result = XInputGetState(index, out _);
            if (result == 1167)
                session = new XboxSession
                {
                    UserIndex = (int)index,
                    BatteryType = XboxBatteryType.Disconnected,
                };
            else if (result != 0)
                session = new XboxSession { UserIndex = (int)index, StateErrorCode = result };
            sessions[index] = session;
        }
        return sessions;
    }

    internal static void ApplyBattery(XboxSession session, byte type, byte level)
    {
        session.BatteryType = type switch
        {
            0 => XboxBatteryType.Disconnected,
            1 => XboxBatteryType.Wired,
            2 => XboxBatteryType.Alkaline,
            3 => XboxBatteryType.NickelMetalHydride,
            _ => XboxBatteryType.Unknown,
        };
        // Battery level is valid only for wireless devices with a known battery type.
        session.BatteryLevel = type is 2 or 3
            ? level switch
            {
                0 => XboxBatteryLevel.Empty,
                1 => XboxBatteryLevel.Low,
                2 => XboxBatteryLevel.Medium,
                3 => XboxBatteryLevel.Full,
                _ => XboxBatteryLevel.Unknown,
            }
            : XboxBatteryLevel.Unknown;
        session.Power = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger,
            RightTrigger;
        public short LeftThumbX,
            LeftThumbY,
            RightThumbX,
            RightThumbY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint PacketNumber;
        public Gamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Vibration
    {
        public ushort LeftMotorSpeed,
            RightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Capabilities
    {
        public byte Type,
            SubType;
        public ushort Flags;
        public Gamepad Gamepad;
        public Vibration Vibration;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BatteryInformation
    {
        public byte Type,
            Level;
    }

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetState(uint userIndex, out State state);

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetCapabilities(
        uint userIndex,
        uint flags,
        out Capabilities capabilities
    );

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetBatteryInformation(
        uint userIndex,
        byte deviceType,
        out BatteryInformation battery
    );
}

/// <summary>每次调用返回一次快照；需要实时状态的客户端可按秒重复调用。</summary>
public sealed class XboxSessionService(IXboxSessionSource source)
    : IPrivilegedService<XboxSessionRequest, XboxSessionReportProcess, RunResult>
{
    public PrivilegedServiceContract<
        XboxSessionRequest,
        XboxSessionReportProcess,
        RunResult
    > Contract => ABIRuntime.Contract.XboxSessionContract;

    public ValueTask<RunResult> ExecuteAsync(
        XboxSessionRequest request,
        IProgress<XboxSessionReportProcess> progress,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var sessions = source.GetSessions();
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new XboxSessionReportProcess(sessions));
            return ValueTask.FromResult(RunResult.Success);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(new RunResult(-1, exception.Message));
        }
    }
}
