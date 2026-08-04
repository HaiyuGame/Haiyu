using System.Runtime.InteropServices;

namespace Haiyu.Pickers;

/// <summary>
/// Uses the desktop common dialogs supplied by Windows itself. Invoke it on the UI thread
/// that owns the supplied window.
/// </summary>
public sealed class NativePickersService(Func<nint> ownerWindowProvider) : IPickersService
{
    private const int BufferLength = 32_768;
    private const int Canceled = unchecked((int)0x800704C7);

    public Task<PickFileResult?> GetFileOpenPicker(IReadOnlyCollection<string> extensions) =>
        ShowFileDialog(extensions, null, requireExistingFile: true);

    public Task<PickFileResult?> GetFileSavePicker(IReadOnlyCollection<string> extensions, string saveName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveName);
        return ShowFileDialog(extensions, saveName, requireExistingFile: false);
    }

    public Task<PickFolderResult?> GetFolderPicker()
    {
        var dialog = NativeMethods.CreateFileOpenDialog();
        try
        {
            var options = NativeMethods.FOS_PICKFOLDERS | NativeMethods.FOS_FORCEFILESYSTEM |
                          NativeMethods.FOS_PATHMUSTEXIST;
            Marshal.ThrowExceptionForHR(dialog.SetOptions(options));

            var result = dialog.Show(ownerWindowProvider());
            if (result == Canceled)
                return Task.FromResult<PickFolderResult?>(null);
            Marshal.ThrowExceptionForHR(result);

            Marshal.ThrowExceptionForHR(dialog.GetResult(out var item));
            try
            {
                Marshal.ThrowExceptionForHR(item.GetDisplayName(NativeMethods.SIGDN_FILESYSPATH, out var path));
                try
                {
                    return Task.FromResult<PickFolderResult?>(new PickFolderResult(Marshal.PtrToStringUni(path)!));
                }
                finally
                {
                    Marshal.FreeCoTaskMem(path);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private Task<PickFileResult?> ShowFileDialog(
        IReadOnlyCollection<string> extensions,
        string? saveName,
        bool requireExistingFile
    )
    {
        ArgumentNullException.ThrowIfNull(extensions);
        if (extensions.Count == 0)
            throw new ArgumentException("At least one file extension is required.", nameof(extensions));

        if (saveName?.Length >= BufferLength)
            throw new ArgumentException($"The file name must be shorter than {BufferLength} characters.", nameof(saveName));

        // Validate and create managed values before allocating unmanaged memory, so validation
        // failures cannot leak a buffer.
        var filterText = CreateFilter(extensions);
        var defaultExtensionText = requireExistingFile ? null : GetDefaultExtension(extensions);
        nint pathBuffer = nint.Zero;
        nint filter = nint.Zero;
        nint defaultExtension = nint.Zero;

        try
        {
            pathBuffer = AllocatePathBuffer(saveName);
            filter = Marshal.StringToHGlobalUni(filterText);
            if (defaultExtensionText is not null)
                defaultExtension = Marshal.StringToHGlobalUni(defaultExtensionText);

            var dialog = new NativeMethods.OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<NativeMethods.OPENFILENAME>(),
                hwndOwner = ownerWindowProvider(),
                lpstrFilter = filter,
                lpstrFile = pathBuffer,
                nMaxFile = BufferLength,
                lpstrDefExt = defaultExtension,
                Flags = NativeMethods.OFN_EXPLORER | NativeMethods.OFN_PATHMUSTEXIST |
                        NativeMethods.OFN_NOCHANGEDIR |
                        (requireExistingFile ? NativeMethods.OFN_FILEMUSTEXIST : NativeMethods.OFN_OVERWRITEPROMPT),
            };

            var accepted = requireExistingFile
                ? NativeMethods.GetOpenFileName(ref dialog)
                : NativeMethods.GetSaveFileName(ref dialog);
            return Task.FromResult(accepted
                ? new PickFileResult(Marshal.PtrToStringUni(pathBuffer)!)
                : HandleFileDialogFailure<PickFileResult>());
        }
        finally
        {
            if (pathBuffer != nint.Zero)
                Marshal.FreeHGlobal(pathBuffer);
            if (filter != nint.Zero)
                Marshal.FreeHGlobal(filter);
            if (defaultExtension != nint.Zero)
                Marshal.FreeHGlobal(defaultExtension);
        }
    }

    private static unsafe nint AllocatePathBuffer(string? initialValue)
    {
        var buffer = Marshal.AllocHGlobal(BufferLength * sizeof(char));
        var characters = new Span<char>((void*)buffer, BufferLength);
        characters.Clear();
        initialValue?.AsSpan().CopyTo(characters);
        return buffer;
    }

    private static T? HandleFileDialogFailure<T>() where T : class
    {
        var error = NativeMethods.CommDlgExtendedError();
        if (error == 0 || error == Canceled)
            return null;

        throw new ExternalException($"Windows file dialog failed (0x{error:X8}).", error);
    }

    private static string CreateFilter(IEnumerable<string> extensions)
    {
        var values = extensions.Select(NormalizeExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var pattern = string.Join(';', values.Select(static extension => $"*{extension}"));
        return $"支持的文件 ({pattern})\0{pattern}\0所有文件 (*.*)\0*.*\0\0";
    }

    private static string GetDefaultExtension(IEnumerable<string> extensions) => NormalizeExtension(extensions.First())[1..];

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}

internal static class NativeMethods
{
    internal const uint OFN_OVERWRITEPROMPT = 0x00000002;
    internal const uint OFN_NOCHANGEDIR = 0x00000008;
    internal const uint OFN_PATHMUSTEXIST = 0x00000800;
    internal const uint OFN_FILEMUSTEXIST = 0x00001000;
    internal const uint OFN_EXPLORER = 0x00080000;
    internal const uint FOS_PICKFOLDERS = 0x00000020;
    internal const uint FOS_FORCEFILESYSTEM = 0x00000040;
    internal const uint FOS_PATHMUSTEXIST = 0x00000800;
    internal const uint SIGDN_FILESYSPATH = 0x80058000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct OPENFILENAME
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public nint lpstrFilter;
        public nint lpstrCustomFilter;
        public uint nMaxCustFilter;
        public uint nFilterIndex;
        public nint lpstrFile;
        public uint nMaxFile;
        public nint lpstrFileTitle;
        public uint nMaxFileTitle;
        public nint lpstrInitialDir;
        public nint lpstrTitle;
        public uint Flags;
        public short nFileOffset;
        public short nFileExtension;
        public nint lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
        public nint pvReserved;
        public uint dwReserved;
        public uint FlagsEx;
    }

    private static readonly Guid FileOpenDialogClsid = new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");

    internal static IFileDialog CreateFileOpenDialog()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Windows File Open dialog is only available on Windows.");

        var type = Type.GetTypeFromCLSID(FileOpenDialogClsid, throwOnError: true)
                   ?? throw new InvalidOperationException("Windows File Open dialog is unavailable.");

        return (IFileDialog)(Activator.CreateInstance(type)
                             ?? throw new InvalidOperationException("Windows File Open dialog could not be created."));
    }

    [ComImport]
    [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileDialog
    {
        [PreserveSig] int Show(nint parent);
        [PreserveSig] int SetFileTypes(uint count, nint filterSpec);
        [PreserveSig] int SetFileTypeIndex(uint index);
        [PreserveSig] int GetFileTypeIndex(out uint index);
        [PreserveSig] int Advise(nint events, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOptions(uint options);
        [PreserveSig] int GetOptions(out uint options);
        [PreserveSig] int SetDefaultFolder(IShellItem folder);
        [PreserveSig] int SetFolder(IShellItem folder);
        [PreserveSig] int GetFolder(out IShellItem folder);
        [PreserveSig] int GetCurrentSelection(out IShellItem item);
        [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int GetFileName(out nint name);
        [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        [PreserveSig] int GetResult(out IShellItem item);
        [PreserveSig] int AddPlace(IShellItem item, uint placement);
        [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        [PreserveSig] int Close(int result);
        [PreserveSig] int SetClientGuid(in Guid guid);
        [PreserveSig] int ClearClientData();
        [PreserveSig] int SetFilter(nint filter);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        [PreserveSig] int BindToHandler(nint bindContext, in Guid bhid, in Guid riid, out nint result);
        [PreserveSig] int GetParent(out IShellItem parent);
        [PreserveSig] int GetDisplayName(uint displayNameType, out nint displayName);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(IShellItem other, uint hint, out int order);
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetOpenFileName(ref OPENFILENAME openFileName);

    [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSaveFileName(ref OPENFILENAME openFileName);

    [DllImport("comdlg32.dll")]
    internal static extern int CommDlgExtendedError();

}
