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
            Marshal.ThrowExceptionForHR(NativeMethods.SetOptions(dialog, options));

            var result = NativeMethods.Show(dialog, ownerWindowProvider());
            if (result == Canceled)
                return Task.FromResult<PickFolderResult?>(null);
            Marshal.ThrowExceptionForHR(result);

            Marshal.ThrowExceptionForHR(NativeMethods.GetResult(dialog, out var item));
            try
            {
                Marshal.ThrowExceptionForHR(
                    NativeMethods.GetDisplayName(item, NativeMethods.SIGDN_FILESYSPATH, out var path));
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
                NativeMethods.Release(item);
            }
        }
        finally
        {
            NativeMethods.Release(dialog);
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

    private const uint CLSCTX_INPROC_SERVER = 0x1;
    private static readonly Guid FileDialogIid = new("42F85136-DB7E-439C-85F1-E4075D135FC8");

    internal static nint CreateFileOpenDialog()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Windows File Open dialog is only available on Windows.");

        Marshal.ThrowExceptionForHR(CoCreateInstance(
            in FileOpenDialogClsid,
            nint.Zero,
            CLSCTX_INPROC_SERVER,
            in FileDialogIid,
            out var dialog));
        return dialog;
    }

    // These calls deliberately use the native vtables rather than an RCW. Activator.CreateInstance
    // and ComImport depend on the runtime's built-in COM interop, which NativeAOT does not provide.
    internal static unsafe int Show(nint dialog, nint owner)
    {
        var vtable = *(nint**)dialog;
        return ((delegate* unmanaged[Stdcall]<nint, nint, int>)vtable[3])(dialog, owner);
    }

    internal static unsafe int SetOptions(nint dialog, uint options)
    {
        var vtable = *(nint**)dialog;
        return ((delegate* unmanaged[Stdcall]<nint, uint, int>)vtable[9])(dialog, options);
    }

    internal static unsafe int GetResult(nint dialog, out nint item)
    {
        var vtable = *(nint**)dialog;
        fixed (nint* itemPointer = &item)
            return ((delegate* unmanaged[Stdcall]<nint, nint*, int>)vtable[20])(dialog, itemPointer);
    }

    internal static unsafe int GetDisplayName(nint item, uint displayNameType, out nint displayName)
    {
        var vtable = *(nint**)item;
        fixed (nint* displayNamePointer = &displayName)
            return ((delegate* unmanaged[Stdcall]<nint, uint, nint*, int>)vtable[5])(
                item, displayNameType, displayNamePointer);
    }

    internal static unsafe void Release(nint instance)
    {
        if (instance == nint.Zero)
            return;

        var vtable = *(nint**)instance;
        _ = ((delegate* unmanaged[Stdcall]<nint, uint>)vtable[2])(instance);
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        in Guid classId,
        nint outer,
        uint context,
        in Guid interfaceId,
        out nint instance);

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetOpenFileName(ref OPENFILENAME openFileName);

    [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSaveFileName(ref OPENFILENAME openFileName);

    [DllImport("comdlg32.dll")]
    internal static extern int CommDlgExtendedError();

}
