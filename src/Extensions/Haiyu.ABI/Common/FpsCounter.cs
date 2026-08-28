using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Tracing.Session;

namespace Haiyu.ABI.Common;

public class FpsCounter:IDisposable
{
    public const int EventID_D3D9PresentStart = 1;
    public const int EventID_DxgiPresentStart = 42;

    Stopwatch watch = null;

    public static readonly Guid DXGI_provider = Guid.Parse(
        "{CA11C036-0102-4A2D-A6AD-F03CFED5D3C9}"
    );
    public static readonly Guid D3D9_provider = Guid.Parse(
        "{783ACA0A-790E-4D7F-8451-AA850511C6B9}"
    );
    public Dictionary<int, TimestampCollection> Frames = new Dictionary<int, TimestampCollection>();
    private Queue<int> avgFpsQueue = new Queue<int>();
    TraceEventSession m_EtwSession;
    private object sync = new();
    private bool avgFpsCheck = false;
    private int fpsCalculate;
    private bool disposedValue;
    private Thread thOutput;

    public Action<Tuple<string, int>> FpsOutput;

    public void Start()
    {
        m_EtwSession = new TraceEventSession("mysess");
        m_EtwSession.StopOnDispose = true;
        m_EtwSession.EnableProvider("Microsoft-Windows-D3D9");
        m_EtwSession.EnableProvider("Microsoft-Windows-DXGI");
        m_EtwSession.Source.AllEvents += Source_AllEvents;
        watch = new Stopwatch();
        watch.Start();

        Thread thETW = new Thread(EtwThreadProc);
        thETW.IsBackground = true;
        thETW.SetApartmentState(ApartmentState.STA);
        thETW.Start();

        thOutput = new Thread(OutputThreadProc);
        thOutput.IsBackground = true;
        thOutput.SetApartmentState(ApartmentState.STA);
        thOutput.Start();
    }

    private void EtwThreadProc(object? obj)
    {
        m_EtwSession.Source.Process();
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("User32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static uint GetWindowDetails()
    {
        uint processId = 0;
        const int intCharCount = 256;

        IntPtr intWindowHandle = IntPtr.Zero;

        StringBuilder strWindowText = new StringBuilder(intCharCount);
        intWindowHandle = GetForegroundWindow();
        GetWindowThreadProcessId(intWindowHandle, out processId);
        return processId;
    }

    void OutputThreadProc(object? obj)
    {
        while (true)
        {
            double to,
                from;
            lock (sync)
            {
                if (Pause)
                    return;
                if (this.disposedValue)
                    return;
                to = watch.Elapsed.TotalMilliseconds;
                from = to - 1000;
                foreach (var x in Frames.Values)
                {
                    if (x.Id == GetWindowDetails())
                    {
                        int count = x.QueryCount(from, to);
                        if (avgFpsCheck) { }
                        else
                        {
                            fpsCalculate = count;
                            FpsOutput?.Invoke(new Tuple<string, int>(x.Name, count));
                        }
                    }
                }
            }
            Thread.Sleep(1000);
        }
    }

    private void Source_AllEvents(Microsoft.Diagnostics.Tracing.TraceEvent obj)
    {
        try
        {
            if (
                ((int) obj.ID == EventID_D3D9PresentStart && obj.ProviderGuid == D3D9_provider)
                || ((int) obj.ID == EventID_DxgiPresentStart && obj.ProviderGuid == DXGI_provider)
            )
            {
                int pid = obj.ProcessID;
                double t;

                t = watch.Elapsed.TotalMilliseconds;

                lock (sync)
                {
                    if (!Frames.ContainsKey(pid))
                    {
                        string name = "";
                        long id = 0;
                        var proc = Process.GetProcessById(pid);
                        if (proc != null)
                        {
                            using (proc)
                            {
                                name = proc.ProcessName;
                                id = proc.Id;
                            }
                        }
                        else
                        {
                            name = pid.ToString();
                        }
                        Frames[pid] = new TimestampCollection(id, name);
                    }
                }
                Frames[pid].Add(t);
            }
        }
        catch (Exception) { }
    }

    public void Stop()
    {
        this.watch.Stop();
    }

    public bool Pause
    {
        get => field;
        set => field = value;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (m_EtwSession != null)
                {
                    this.m_EtwSession.Dispose();
                    this.m_EtwSession = null;
                }
                if (watch != null)
                {
                    watch.Stop();
                    watch = null;
                }
            }

            disposedValue = true;
        }
    }

    // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
    // ~FPSCounter()
    // {
    //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
