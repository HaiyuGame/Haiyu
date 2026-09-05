using WinUIEx.Messaging;

namespace Haiyu.Controls;

public partial class NotifyIconWinUI : Control
{

    #region Evemt
    public delegate void LeftClickDelegate(object sender, EventArgs args);
    public delegate void LeftDoubleClickDelegate(object sender, EventArgs args);
    public delegate void RightClickDelegate(object sender, EventArgs args);

    public event LeftClickDelegate LeftClick
    {
        add => leftClickDelegate += value;
        remove => leftClickDelegate -= value;
    }
    public event RightClickDelegate RightClick
    {
        add => rightClickDelegate += value;
        remove => rightClickDelegate -= value;
    }
    public event LeftDoubleClickDelegate LeftDoubleClick
    {
        add => leftDoubleClickDelegate += value;
        remove => leftDoubleClickDelegate -= value;
    }
    private LeftClickDelegate leftClickDelegate;
    private LeftDoubleClickDelegate leftDoubleClickDelegate;
    private RightClickDelegate rightClickDelegate;
    #endregion

    private IntPtr _iconHandle;
    private WindowMessageMonitor monitor;
    private const int WM_TRAYICON = 0x0400 + 20;
    public const int NIF_MESSAGE = 0x00000001;
    public const int NIF_ICON = 0x00000002;
    public const int NIF_TIP = 0x00000004;
    public const int NIM_ADD = 0x00000000;
    public const int NIM_MODIFY = 0x00000001;
    public const int NIM_DELETE = 0x00000002;
    private const int DoubleClickInterval = 300;
    private Timer clickTimer;
    private bool doubleClickTriggered;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeout;
        public uint uVersion;
        public uint dwInfoFlags;
        public Guid guidItem;
        public uint uVersion2;
        public IntPtr hBalloonIcon;
    }

    public enum Shell_NotifyIconType : int
    {
        Add = 0,
        Modify = 1,
        Delete = 2,
        SetFocus = 3,
        Version = 4,
    }

    public const int IMAGE_ICON = 1;
    public const int LR_LOADFROMFILE = 0x00000010;
    public const int LR_DEFAULTSIZE = 0x00000040;
    public const int LR_LOADTRANSPARENT = 0x00000020;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern bool Shell_NotifyIcon(
        Shell_NotifyIconType dwMessage,
        ref NOTIFYICONDATA lpData
    );

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr LoadImage(
        IntPtr hInst,
        string lpszName,
        uint uType,
        int cxDesired,
        int cyDesired,
        uint fuLoad
    );

    public NotifyIconMenu ContextMenu
    {
        get { return (NotifyIconMenu)GetValue(ContextMenuProperty); }
        set { SetValue(ContextMenuProperty, value); }
    }

    public WindowEx Window { get; private set; }

    public static readonly DependencyProperty ContextMenuProperty = DependencyProperty.Register(
        "ContextMenu",
        typeof(NotifyIconMenu),
        typeof(NotifyIconWinUI),
        new PropertyMetadata(null)
    );

    public const int WM_LBUTTONDBLCLK = 0x0203; // 左键双击
    public const int WM_RBUTTONDBLCLK = 0x0206; // 右键双击
    public const int WM_LBUTTONDOWN = 0x0201; // 左键单击
    public const int WM_RBUTTONDOWN = 0x0204; // 右键单击

    [DllImport("user32.dll")]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    public static extern bool AppendMenu(
        IntPtr hMenu,
        uint uFlags,
        uint uIDNewItem,
        string lpNewItem
    );

    [DllImport("user32.dll")]
    public static extern uint TrackPopupMenuEx(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        IntPtr hwnd,
        IntPtr lptpm
    );

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public void CreateTrayIcon(string ico, string tipMessage)
    {
        _iconHandle = LoadImage(
            IntPtr.Zero,
            ico,
            IMAGE_ICON,
            48,
            48,
            LR_LOADFROMFILE | LR_DEFAULTSIZE
        );

        if (_iconHandle != IntPtr.Zero)
        {
            var hWnd = (nint)this.Window.AppWindow.Id.Value;
            var nid = new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hWnd,
                uID = 1,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _iconHandle,
                szTip = tipMessage,
            };
            Shell_NotifyIcon(Shell_NotifyIconType.Add, ref nid);
        }
    }

    public void RegisterWin(WindowEx window)
    {
        this.Window = window;
        monitor = new WindowMessageMonitor(this.Window);
        monitor.WindowMessageReceived += OnWindowMessageReceived;
    }

    private void OnWindowMessageReceived(object sender, WindowMessageEventArgs e)
    {
        if (e.Message.MessageId == WM_TRAYICON)
        {
            switch (e.Message.LParam)
            {
                case WM_LBUTTONDOWN:
                    if (clickTimer == null)
                    {
                        clickTimer = new Timer(
                            SingleClickAction,
                            null,
                            DoubleClickInterval,
                            Timeout.Infinite
                        );
                    }
                    break;
                case WM_LBUTTONDBLCLK:
                    doubleClickTriggered = true;
                    clickTimer?.Dispose();
                    clickTimer = null;
                    Debug.WriteLine("左键双击");
                    this.leftDoubleClickDelegate?.Invoke(this, new());
                    break;
                case WM_RBUTTONDOWN:
                    Debug.WriteLine("右键");
                    this.rightClickDelegate?.Invoke(this, new());
                    ShowContextMenu();
                    break;
                case WM_RBUTTONDBLCLK:
                    break;
            }
        }
    }

    public const int TPM_LEFTALIGN = 0x0000;
    public const int TPM_RIGHTBUTTON = 0x0002;
    public const int TPM_BOTTOMALIGN = 0x0020;
    public const int TPM_RETURNCMD = 0x0100;

    private void ShowContextMenu()
    {
        if (ContextMenu == null)
            return;
        IntPtr hMenu = CreatePopupMenu();
        for (int i = 0; i < ContextMenu.Items.Count; i++)
        {
            AppendMenu(hMenu, 0, (uint)i + 1, ContextMenu.Items[i].Header);
        }

        var cursorPos = WindowExtension.GetCursorPos(out var PoInt);
        IntPtr hWnd = WindowNative.GetWindowHandle(
            this.Window
        );

        if (hWnd == IntPtr.Zero)
        {
            return;
        }


        uint cmd = TrackPopupMenuEx(
            hMenu,
            TPM_LEFTALIGN | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN | TPM_RETURNCMD,
            PoInt.X,
            PoInt.Y,
            hWnd,
            IntPtr.Zero
        );

        if (cmd != 0)
        {
            if (ContextMenu.Items[(int)cmd - 1].Command != null)
            {
                ContextMenu.Items[(int)cmd - 1].Command.Execute(null);
            }
        }

        PostMessage(hWnd, (uint)0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    private void SingleClickAction(object state)
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (!doubleClickTriggered)
            {
                this.leftClickDelegate?.Invoke(this, new());
            }
            doubleClickTriggered = false;
            clickTimer = null;
        });
    }
}
