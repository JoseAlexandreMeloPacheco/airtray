using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// The supplied image is a two-state sprite sheet. Crops are applied at runtime;
// the original artwork is embedded, with no dependency on a user's file path.
internal sealed class Artwork : IDisposable
{
    readonly BitmapSource source;
    internal BitmapSource Off { get; }
    internal BitmapSource On { get; }
    internal System.Drawing.Icon OffIcon { get; }
    internal System.Drawing.Icon OnIcon { get; }

    internal Artwork()
    {
        using var stream = typeof(Artwork).Assembly.GetManifestResourceStream("ElginTray.Assets.air-states-transparent.png")!;
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        source = decoder.Frames[0]; source.Freeze();
        Off = Crop(.055, .10, .425, .85);
        On = Crop(.50, .10, .45, .85);
        OffIcon = MakeIcon(Crop(.105, .30, .36, .35));
        OnIcon = MakeIcon(Crop(.545, .30, .36, .35));
    }

    BitmapSource Crop(double x, double y, double width, double height)
    {
        var crop = new CroppedBitmap(source, new Int32Rect((int)(source.PixelWidth*x), (int)(source.PixelHeight*y), (int)(source.PixelWidth*width), (int)(source.PixelHeight*height)));
        crop.Freeze(); return crop;
    }

    static System.Drawing.Icon MakeIcon(BitmapSource image)
    {
        var visual = new DrawingVisual();
        using(var drawing = visual.RenderOpen()) drawing.DrawImage(image, new Rect(0, 8, 32, 16));
        var bitmap = new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream(); encoder.Save(stream); stream.Position=0;
        using var native = new System.Drawing.Bitmap(stream);
        var handle = native.GetHicon();
        try { using var borrowed = System.Drawing.Icon.FromHandle(handle); return (System.Drawing.Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyIcon(IntPtr handle);
    public void Dispose() { OffIcon.Dispose(); OnIcon.Dispose(); }
}
