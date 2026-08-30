using System;
using System.Collections.Generic;
using System.Text;

namespace Haiyu.Common.Bases
{
    internal sealed class WindowMessageMonitor(nint hwnd) : IDisposable
    {
        private readonly nuint _id = unchecked((nuint) hwnd.GetHashCode());
        private readonly SubclassProc _proc = OnMessageStatic;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<nuint, WindowMessageMonitor> Instances = new();
        internal event EventHandler<WindowMessageEventArgs>? MessageReceived;
        internal void Attach() { Instances[_id] = this; if (!SetWindowSubclass(hwnd, _proc, _id, 0)) throw new InvalidOperationException($"SetWindowSubclass: {Marshal.GetLastWin32Error()}"); }
        public void Dispose() { RemoveWindowSubclass(hwnd, _proc, _id); Instances.TryRemove(_id, out _); }
        private static nint OnMessageStatic(nint h, uint m, nuint w, nint l, nuint id, nint data) { WindowMessageEventArgs e = new(h, m, w); if (Instances.TryGetValue(id, out var x)) x.MessageReceived?.Invoke(x, e); return e.Handled ? e.Result : DefSubclassProc(h, m, w, l); }
        private delegate nint SubclassProc(nint h, uint m, nuint w, nint l, nuint id, nint data);
        [DllImport("comctl32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowSubclass(nint h, SubclassProc p, nuint id, nint d);
        [DllImport("comctl32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(nint h, SubclassProc p, nuint id);
        [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint h, uint m, nuint w, nint l);
    }
    internal sealed class WindowMessageEventArgs(nint hwnd, uint messageId, nuint wParam) : EventArgs { internal nint Hwnd { get; } = hwnd; internal uint MessageId { get; } = messageId; internal nuint WParam { get; } = wParam; internal bool Handled { get; set; } internal nint Result { get; set; } }

}
