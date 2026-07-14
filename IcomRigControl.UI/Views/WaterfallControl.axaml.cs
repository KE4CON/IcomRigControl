using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace IcomRigControl.UI.Views;

public partial class WaterfallControl : UserControl
{
    private const int DataPoints = 475;

    private WriteableBitmap? _bitmap;
    private byte[]? _pixelBuffer;
    private int _bitmapHeight = 200;

    public WaterfallControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => EnsureBitmapSized();
        LayoutUpdated += (_, _) => EnsureBitmapSized();
    }

   private void EnsureBitmapSized()
    {
        int newHeight = (int)Math.Max(50, Bounds.Height);
        if (_bitmap != null && newHeight == _bitmapHeight) return;

        _bitmapHeight = newHeight;
        _bitmap = new WriteableBitmap(
            new PixelSize(DataPoints, _bitmapHeight),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        _pixelBuffer = new byte[DataPoints * _bitmapHeight * 4];

        WaterfallImage.Source = _bitmap;
    }

    public void PushSweep(int[] levels)
    {
        if (_bitmap == null || _pixelBuffer == null)
        {
            EnsureBitmapSized();
            if (_bitmap == null || _pixelBuffer == null) return;
        }

        int rowBytes = DataPoints * 4;
        Array.Copy(_pixelBuffer, 0, _pixelBuffer, rowBytes, _pixelBuffer.Length - rowBytes);

        for (int x = 0; x < DataPoints; x++)
        {
            int level = x < levels.Length ? levels[x] : 0;
            var (b, g, r) = LevelToColor(level);

            int offset = x * 4;
            _pixelBuffer[offset]     = b;
            _pixelBuffer[offset + 1] = g;
            _pixelBuffer[offset + 2] = r;
            _pixelBuffer[offset + 3] = 255;
        }

        using (var fb = _bitmap.Lock())
        {
            System.Runtime.InteropServices.Marshal.Copy(_pixelBuffer, 0, fb.Address, _pixelBuffer.Length);
        }

        WaterfallImage.InvalidateVisual();
        InvalidateVisual();
        InvalidateArrange();
        InvalidateMeasure();
    }

    private static (byte b, byte g, byte r) LevelToColor(int level)
    {
        double t = Math.Clamp(level / 255.0, 0, 1);

        if (t < 0.25)
        {
            double s = t / 0.25;
            return ((byte)(s * 255), 0, 0);
        }
        else if (t < 0.5)
        {
            double s = (t - 0.25) / 0.25;
            return ((byte)((1 - s) * 255), (byte)(s * 255), 0);
        }
        else if (t < 0.75)
        {
            double s = (t - 0.5) / 0.25;
            return (0, 255, (byte)(s * 255));
        }
        else
        {
            double s = (t - 0.75) / 0.25;
            return (0, (byte)((1 - s) * 255), 255);
        }
    }
}