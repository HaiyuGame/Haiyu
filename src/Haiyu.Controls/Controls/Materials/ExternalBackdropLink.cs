using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using WinRT;

namespace Haiyu.Controls;

/// <summary>
/// Minimal C# ABI adapter for the runtime's ContentExternalBackdropLink.
/// This runtime type is absent from the SDK's public managed projection.
/// IIDs and vtable order were read from Microsoft.UI.winmd, runtime 2.6.5.
/// Requires validation when changing the Windows App SDK/runtime version.
/// </summary>
internal sealed unsafe class ExternalBackdropLink : IDisposable
{
    private nint instance;
    private readonly ICompositionSupportsSystemBackdrop backdropTarget;
    internal Visual PlacementVisual { get; }
    internal Windows.UI.Composition.CompositionBrush? SystemBackdrop
    {
        set => backdropTarget.SystemBackdrop = value!;
    }
    internal CompositionBorderMode ExternalBackdropBorderMode
    {
        set => Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int, int>)Vtable(instance)[8])(instance, (int)value));
    }

    private ExternalBackdropLink(nint value)
    {
        // Create returns the default IContentExternalBackdropLink interface.
        instance = value;
        nint visual = 0;
        Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Vtable(instance)[9])(instance, &visual));
        try { PlacementVisual = MarshalInspectable<Visual>.FromAbi(visual); }
        finally { if (visual != 0) Marshal.Release(visual); }

        var iid = GuidGenerator.GetIID(typeof(ICompositionSupportsSystemBackdrop));
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(instance, in iid, out var target));
        try { backdropTarget = MarshalInterface<ICompositionSupportsSystemBackdrop>.FromAbi(target); }
        finally { Marshal.Release(target); }
    }

    internal static ExternalBackdropLink Create(Compositor compositor)
    {
        nint name = 0, factory = 0, result = 0;
        var iid = new Guid("46cac6fb-bb51-510a-958d-e0eb4160f678");
        const string className = "Microsoft.UI.Content.ContentExternalBackdropLink";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out name));
        nint compositorAbi = 0;
        try
        {
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, in iid, out factory));
            compositorAbi = MarshalInspectable<Compositor>.FromManaged(compositor);
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Vtable(factory)[6])(factory, compositorAbi, &result));
            var link = new ExternalBackdropLink(result);
            result = 0; // ownership transferred
            return link;
        }
        finally
        {
            if (result != 0) Marshal.Release(result);
            if (compositorAbi != 0) MarshalInspectable<Compositor>.DisposeAbi(compositorAbi);
            if (factory != 0) Marshal.Release(factory);
            WindowsDeleteString(name);
        }
    }

    public void Dispose()
    {
        var value = instance;
        if (value == 0) return;
        instance = 0;
        var iid = new Guid("30d5a829-7fa4-4026-83bb-d75bae4ea99e"); // IClosable
        try
        {
            if (Marshal.QueryInterface(value, in iid, out var close) >= 0)
            {
                try { Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint, int>)Vtable(close)[6])(close)); }
                finally { Marshal.Release(close); }
            }
        }
        finally { Marshal.Release(value); }
    }

    private static nint* Vtable(nint obj) => *(nint**)obj;
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(nint name, in Guid iid, out nint factory);
    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out nint value);
    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint value);
}

