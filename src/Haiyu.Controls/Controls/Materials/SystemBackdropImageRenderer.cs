using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using WinRT;
using WC = Windows.UI.Composition;
using MC = Microsoft.UI.Composition;

namespace Haiyu.Controls;

// Renders background images in system Composition so external acrylic can sample
// them, just like video surfaces. No per-acrylic background-type configuration.
internal sealed class SystemBackdropImageRenderer : IDisposable
{
    private readonly FrameworkElement host;
    private CanvasDevice? device;
    private CanvasBitmap? bitmap;
    private WC.Compositor? compositor;
    private WC.CompositionGraphicsDevice? graphics;
    private WC.CompositionDrawingSurface? surface;
    private WC.CompositionSurfaceBrush? brush;
    private WC.SpriteVisual? sprite;
    private WC.CompositionTarget? target;
    private MC.Visual? placement;
    private nint output;
    private bool attached;
    private bool disposed;

    private SystemBackdropImageRenderer(FrameworkElement host) => this.host = host;

    internal static async Task<SystemBackdropImageRenderer> CreateAsync(FrameworkElement host, string path)
    {
        var renderer = new SystemBackdropImageRenderer(host);
        try
        {
            renderer.device = new CanvasDevice();
            renderer.bitmap = Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile
                ? await CanvasBitmap.LoadAsync(renderer.device, uri)
                : await CanvasBitmap.LoadAsync(renderer.device, uri?.LocalPath ?? path);
            renderer.Initialize();
            return renderer;
        }
        catch
        {
            renderer.Dispose();
            throw;
        }
    }

    private void Initialize()
    {
        compositor = new WC.Compositor();
        graphics = CreateGraphics(compositor, device!);
        var size = bitmap!.SizeInPixels;
        surface = graphics.CreateDrawingSurface(new Windows.Foundation.Size(size.Width, size.Height),
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
            Windows.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
        Draw(surface, bitmap, size.Width, size.Height);
        bitmap.Dispose();
        bitmap = null;
        brush = compositor.CreateSurfaceBrush(surface);
        brush.Stretch = WC.CompositionStretch.UniformToFill;
        sprite = compositor.CreateSpriteVisual();
        sprite.Brush = brush;
        CreateOutput();
        target!.Root = sprite;
    }

    internal void Attach(Microsoft.UI.Xaml.Media.Stretch stretch)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (attached) return;
        attached = true;
        SetStretch(stretch);
        host.SizeChanged += Resize;
        Resize(null, null!);
        ElementCompositionPreview.SetElementChildVisual(host, placement);
    }

    internal void SetStretch(Microsoft.UI.Xaml.Media.Stretch stretch)
    {
        if (brush is not null) brush.Stretch = stretch switch
        {
            Microsoft.UI.Xaml.Media.Stretch.None => WC.CompositionStretch.None,
            Microsoft.UI.Xaml.Media.Stretch.Fill => WC.CompositionStretch.Fill,
            Microsoft.UI.Xaml.Media.Stretch.Uniform => WC.CompositionStretch.Uniform,
            _ => WC.CompositionStretch.UniformToFill,
        };
    }

    private void Resize(object? sender, SizeChangedEventArgs e)
    {
        var size = new Vector2((float)host.ActualWidth, (float)host.ActualHeight);
        if (sprite is not null) sprite.Size = size;
        if (placement is not null) placement.Size = size;
    }

    private static unsafe nint* V(nint p) => *(nint**)p;
    private static nint Query(object value, string iid)
    {
        var abi = MarshalInspectable<object>.FromManaged(value);
        try { return Query(abi, iid); }
        finally { MarshalInspectable<object>.DisposeAbi(abi); }
    }
    private static nint Query(nint value, string iid)
    {
        var guid = new Guid(iid);
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(value, in guid, out var result));
        return result;
    }
    private static unsafe nint Native(object value, string iid)
    {
        var wrapper = Query(value, "5F10688D-EA55-4D55-A3B0-4DDB55C0C20A");
        try
        {
            var guid = new Guid(iid);
            nint result = 0;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint,nint,float,Guid*,nint*,int>)V(wrapper)[3])
                (wrapper,0,96,&guid,&result));
            return result;
        }
        finally { Marshal.Release(wrapper); }
    }
    private static unsafe WC.CompositionGraphicsDevice CreateGraphics(WC.Compositor compositor, CanvasDevice device)
    {
        var interop = Query(compositor, "25297D5C-3AD4-4C9C-B5CF-E36A38512330");
        nint d2d = 0, result = 0;
        try
        {
            d2d = Native(device, "47DD575D-AC05-4CDD-8049-9B02CD16F44C");
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint,nint,nint*,int>)V(interop)[5])(interop,d2d,&result));
            return MarshalInspectable<WC.CompositionGraphicsDevice>.FromAbi(result);
        }
        finally { if(result != 0) Marshal.Release(result); if(d2d != 0) Marshal.Release(d2d); Marshal.Release(interop); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public float Left,Top,Right,Bottom; }
    private static unsafe void Draw(WC.CompositionDrawingSurface surface, CanvasBitmap bitmap, uint width, uint height)
    {
        var interop = Query(surface, "FD04E6E3-FE0C-4C3C-AB19-A07601A576EE");
        nint nativeBitmap = 0, context = 0;
        var guid = new Guid("E8F7FE7A-191C-466D-AD95-975678BDA998");
        Point offset = default;
        try
        {
            nativeBitmap = Native(bitmap, "A2296057-EA42-4099-983B-539FB6505426");
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint,nint,Guid*,nint*,Point*,int>)V(interop)[3])
                (interop,0,&guid,&context,&offset));
            try
            {
                ((delegate* unmanaged[Stdcall]<nint,float,float,void>)V(context)[51])(context,96,96);
                var rect = new Rect { Left=offset.X, Top=offset.Y, Right=offset.X+width, Bottom=offset.Y+height };
                ((delegate* unmanaged[Stdcall]<nint,nint,Rect*,float,int,nint,void>)V(context)[26])
                    (context,nativeBitmap,&rect,1,1,0);
            }
            finally { Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint,int>)V(interop)[4])(interop)); }
        }
        finally { if(context != 0) Marshal.Release(context); if(nativeBitmap != 0) Marshal.Release(nativeBitmap); Marshal.Release(interop); }
    }
    private unsafe void CreateOutput()
    {
        const string name="Microsoft.UI.Content.ContentExternalOutputLink";
        nint hstring=0,factory=0;
        var iid=new Guid("B758F401-833E-587D-B0CD-A3934EBA3721");
        var compositorAbi=MarshalInspectable<MC.Compositor>.FromManaged(Microsoft.UI.Xaml.Media.CompositionTarget.GetCompositorForCurrentThread());
        try
        {
            Marshal.ThrowExceptionForHR(WindowsCreateString(name,name.Length,out hstring));
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring,in iid,out factory));
            nint result=0;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint,nint,nint*,int>)V(factory)[6])(factory,compositorAbi,&result));
            output=result;
            nint visual=0;
            Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint,nint*,int>)V(output)[13])(output,&visual));
            try { placement=MarshalInspectable<MC.Visual>.FromAbi(visual); }
            finally { Marshal.Release(visual); }
            var targetAbi=Query(output,"a1bea8ba-d726-4663-8129-6b5e7927ffa6");
            try { target=MarshalInspectable<WC.CompositionTarget>.FromAbi(targetAbi); }
            finally { Marshal.Release(targetAbi); }
        }
        finally { MarshalInspectable<MC.Compositor>.DisposeAbi(compositorAbi); if(factory!=0) Marshal.Release(factory); WindowsDeleteString(hstring); }
    }
    public unsafe void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (attached)
        {
            host.SizeChanged -= Resize;
            ElementCompositionPreview.SetElementChildVisual(host, null);
            attached = false;
        }
        if (target is not null) target.Root = null;
        // Close the link before releasing visuals and its system compositor.
        if (output != 0)
        {
            var value = output;
            output = 0;
            try
            {
                var close = Query(value, "30d5a829-7fa4-4026-83bb-d75bae4ea99e");
                try { Marshal.ThrowExceptionForHR(((delegate* unmanaged[Stdcall]<nint,int>)V(close)[6])(close)); }
                finally { Marshal.Release(close); }
            }
            finally { Marshal.Release(value); }
        }
        target?.Dispose(); target = null;
        placement?.Dispose(); placement = null;
        sprite?.Dispose(); sprite = null;
        brush?.Dispose(); brush = null;
        surface?.Dispose(); surface = null;
        graphics?.Dispose(); graphics = null;
        compositor?.Dispose(); compositor = null;
        bitmap?.Dispose(); bitmap = null;
        device?.Dispose(); device = null;
    }
    [DllImport("combase.dll",CharSet=CharSet.Unicode)] private static extern int WindowsCreateString(string value,int length,out nint result);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint name,in Guid iid,out nint result);
}

