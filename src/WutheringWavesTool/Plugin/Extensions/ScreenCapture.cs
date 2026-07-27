using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Haiyu.Plugin.Extensions;

public static class ScreenCapture
{
    public static byte[] CaptureScreenPixels(out int screenWidth, out int screenHeight)
    {
        IntPtr hScreenDC = IntPtr.Zero;
        IntPtr hMemoryDC = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;
        IntPtr pBmi = IntPtr.Zero;
        byte[] pixelData = null;
        screenWidth = 0;
        screenHeight = 0;

        try
        {
            hScreenDC = NativeMethods.CreateDC("DISPLAY", null, null, IntPtr.Zero);
            if (hScreenDC == IntPtr.Zero)
            {
                throw new Exception(LanguageService.GetStringByText("无法获取屏幕设备上下文！"));
            }
            screenWidth = NativeMethods.GetDeviceCaps(hScreenDC, NativeMethods.HORZRES);
            screenHeight = NativeMethods.GetDeviceCaps(hScreenDC, NativeMethods.VERTRES);
            hMemoryDC = NativeMethods.CreateCompatibleDC(hScreenDC);
            if (hMemoryDC == IntPtr.Zero)
            {
                throw new Exception(LanguageService.GetStringByText("无法创建内存设备上下文！"));
            }
            hBitmap = NativeMethods.CreateCompatibleBitmap(hScreenDC, screenWidth, screenHeight);
            if (hBitmap == IntPtr.Zero)
            {
                throw new Exception(LanguageService.GetStringByText("无法创建位图！"));
            }
            hOldBitmap = NativeMethods.SelectObject(hMemoryDC, hBitmap);

            if (
                !NativeMethods.BitBlt(
                    hMemoryDC,
                    0,
                    0,
                    screenWidth,
                    screenHeight,
                    hScreenDC,
                    0,
                    0,
                    NativeMethods.SRCCOPY
                )
            )
            {
                throw new Exception(LanguageService.GetStringByText("拷贝屏幕内容失败！"));
            }

            NativeMethods.SelectObject(hMemoryDC, hOldBitmap);
            hOldBitmap = IntPtr.Zero;
            NativeMethods.BITMAPINFOHEADER bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER)),
                biWidth = screenWidth,
                biHeight = -screenHeight, 
                biPlanes = 1,
                biBitCount = 32, 
                biCompression = 0,
            };

            int stride = ((screenWidth * 32) + 31) / 32 * 4;
            int bufferSize = stride * screenHeight;
            pixelData = new byte[bufferSize];
            pBmi = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeMethods.BITMAPINFO)));
            Marshal.StructureToPtr(bmiHeader, pBmi, false);

            int linesCopied = NativeMethods.GetDIBits(
                hScreenDC,
                hBitmap,
                0,
                (uint)screenHeight,
                pixelData,
                pBmi,
                NativeMethods.DIB_RGB_COLORS
            );

            if (linesCopied == 0)
            {
                throw new Exception(LanguageService.FormatByText(LanguageService.GetStringByText("GetDIBits 调用失败! 错误码: {0}"), Marshal.GetLastWin32Error()));
            }

            return pixelData;
        }
        catch (Exception ex)
        {
            NativeMethods.MessageBox(IntPtr.Zero, ex.Message, LanguageService.GetStringByText("错误"), 0);
            return null;
        }
        finally
        {
            if (pBmi != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pBmi);
            }
            if (hBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(hBitmap);
            }
            if (hMemoryDC != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(hMemoryDC);
            }
            if (hScreenDC != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(hScreenDC);
            }
        }
    }

    public static async Task SaveRawPixelsToFileAsync(
        byte[] rawPixels,
        int screenWidth,
        int screenHeight,
        string file,
        Guid encoderId
    )
    {
        if (rawPixels == null)
        {
            throw new ArgumentNullException(nameof(rawPixels));
        }
        if (string.IsNullOrEmpty(file))
        {
            throw new ArgumentException(LanguageService.GetStringByText("文件路径不能为空。"), nameof(file));
        }
        using (var encodedStream = new InMemoryRandomAccessStream())
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, encodedStream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)screenWidth,
                (uint)screenHeight,
                96.0,
                96.0,
                rawPixels
            );
            await encoder.FlushAsync();
            encodedStream.Seek(0);
            using (var fileStream = File.Create(file))
            {
                using (var dotNetStream = encodedStream.AsStreamForRead())
                {
                    await dotNetStream.CopyToAsync(fileStream);
                }
            }
        }
    }
}
