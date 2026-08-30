namespace Haiyu.Common;

public static class Win32
{
    // 定义常量
    const int SM_CXWORKAREA = 0x0028; // 工作区宽度
    const int SM_CYWORKAREA = 0x0029; // 工作区高度

    // 导入GetSystemMetrics函数
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32", EntryPoint = "SetLayeredWindowAttributes")]
    public static extern int SetLayeredWindowAttributes(
        IntPtr hwnd,
        int crKey,
        int bAlpha,
        int dwFlags
    );

    public const uint SPI_GETWORKAREA = 0x0030;

    // 导入SystemParametersInfo函数
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        ref RECT pvParam,
        uint fWinIni
    );
}
