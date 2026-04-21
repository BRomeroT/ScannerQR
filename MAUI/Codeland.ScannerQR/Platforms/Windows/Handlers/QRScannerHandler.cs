using Codeland.ScannerQR.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using ZXing;
using ZXing.Common;
using System.Runtime.InteropServices.WindowsRuntime;
using WinGrid = Microsoft.UI.Xaml.Controls.Grid;
using WinImage = Microsoft.UI.Xaml.Controls.Image;

namespace Codeland.ScannerQR.Handlers;

public partial class QRScannerHandler : ViewHandler<QRScannerView, WinGrid>
{
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private WinImage? _previewImage;
    private SoftwareBitmapSource? _bitmapSource;
    private ZXing.BarcodeReaderGeneric? _barcodeReader;
    private bool _isRunning;
    private SoftwareBitmap? _backBuffer;
    private bool _renderTaskRunning;
    private bool _isShuttingDown;

    protected override WinGrid CreatePlatformView()
    {
        var grid = new WinGrid();
        _previewImage = new WinImage
        {
            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch
        };
        grid.Children.Add(_previewImage);
        return grid;
    }

    protected override void ConnectHandler(WinGrid platformView)
    {
        base.ConnectHandler(platformView);
        _isShuttingDown = false;

        _barcodeReader = new ZXing.BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true
            }
        };

        if (VirtualView.AutoStart)
            _ = StartCameraAsync();
    }

    protected override void DisconnectHandler(WinGrid platformView)
    {
        _isShuttingDown = true;
        _ = StopCameraAsync(raiseStatus: false);
        base.DisconnectHandler(platformView);
    }

    partial void StartCamera() => _ = StartCameraAsync();
    partial void StopCamera() => _ = StopCameraAsync();

    partial void SetZoom(double zoom)
    {
        if (_isShuttingDown || _mediaCapture?.VideoDeviceController?.ZoomControl == null) return;

        var zoomControl = _mediaCapture.VideoDeviceController.ZoomControl;
        if (!zoomControl.Supported) return;

        var clamped = Math.Clamp(zoom, zoomControl.Min, zoomControl.Max);
        zoomControl.Value = (float)clamped;
        VirtualView?.RaiseZoomChanged(clamped);
    }

    private async Task StartCameraAsync()
    {
        if (_isRunning || _isShuttingDown || VirtualView == null) return;

        try
        {
            VirtualView.RaiseScanStatus("Starting camera...");

            var sourceGroups = await MediaFrameSourceGroup.FindAllAsync();
            if (_isShuttingDown || VirtualView == null) return;

            var sourceGroup = sourceGroups.FirstOrDefault(g =>
                g.SourceInfos.Any(si => si.SourceKind == MediaFrameSourceKind.Color));

            if (sourceGroup == null)
            {
                VirtualView.RaiseScanStatus("No camera found");
                return;
            }

            _mediaCapture = new MediaCapture();

            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MediaCategory = MediaCategory.Other,
                SourceGroup = sourceGroup,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };

            await _mediaCapture.InitializeAsync(settings);
            if (_isShuttingDown || VirtualView == null) return;

            var colorSource = _mediaCapture.FrameSources.Values
                .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color);

            if (colorSource == null)
            {
                VirtualView.RaiseScanStatus("No color camera source found");
                return;
            }

            // Create frame reader — let the system choose the best format, we convert later
            _frameReader = await _mediaCapture.CreateFrameReaderAsync(colorSource);
            _frameReader.FrameArrived += OnFrameArrived;
            var status = await _frameReader.StartAsync();

            if (_isShuttingDown || VirtualView == null) return;

            if (status != MediaFrameReaderStartStatus.Success)
            {
                VirtualView.RaiseScanStatus($"Frame reader failed: {status}");
                return;
            }

            // Create bitmap source on UI thread
            _bitmapSource = new SoftwareBitmapSource();
            if (_previewImage != null)
                _previewImage.Source = _bitmapSource;

            _isRunning = true;
            VirtualView.IsRunning = true;
            VirtualView.RaiseScanStatus("Camera started");

            var zoomControl = _mediaCapture.VideoDeviceController?.ZoomControl;
            if (zoomControl?.Supported == true)
                VirtualView.RaiseZoomChanged(zoomControl.Value);
        }
        catch (UnauthorizedAccessException)
        {
            VirtualView?.RaiseScanStatus("Camera access denied. Check app permissions.");
        }
        catch (Exception ex)
        {
            VirtualView?.RaiseScanStatus($"Camera error: {ex.Message}");
        }
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_isShuttingDown || VirtualView == null) return;

        try
        {
            using var frameRef = sender.TryAcquireLatestFrame();
            if (frameRef?.VideoMediaFrame == null) return;

            // Get SoftwareBitmap (guaranteed with MemoryPreference.Cpu)
            var frameBitmap = frameRef.VideoMediaFrame.SoftwareBitmap;
            if (frameBitmap == null) return;

            // Convert to BGRA8/Premultiplied (required by SoftwareBitmapSource)
            var converted = (frameBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                             frameBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                ? SoftwareBitmap.Convert(frameBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied)
                : SoftwareBitmap.Copy(frameBitmap);

            // QR decode (on this background thread)
            try
            {
                var width = converted.PixelWidth;
                var height = converted.PixelHeight;
                var buffer = new byte[4 * width * height];
                converted.CopyToBuffer(buffer.AsBuffer());

                var luminanceSource = new ZXing.RGBLuminanceSource(
                    buffer, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);

                var result = _barcodeReader?.Decode(luminanceSource);
                if (result != null && !string.IsNullOrEmpty(result.Text) && !_isShuttingDown)
                    VirtualView?.RaiseQRDetected(result.Text);
            }
            catch { }

            // Swap into back buffer (thread-safe); dispose the old one
            var old = Interlocked.Exchange(ref _backBuffer, converted);
            old?.Dispose();

            // Schedule UI render
            var previewImage = _previewImage;
            var dispatcherQueue = previewImage?.DispatcherQueue;
            if (dispatcherQueue == null) return;

            dispatcherQueue.TryEnqueue(async () =>
            {
                if (_renderTaskRunning || _isShuttingDown) return;
                _renderTaskRunning = true;

                try
                {
                    // Drain back buffer — always render the latest frame
                    SoftwareBitmap? bitmap;
                    while (!_isShuttingDown && (bitmap = Interlocked.Exchange(ref _backBuffer, null)) != null)
                    {
                        try
                        {
                            if (_bitmapSource != null)
                                await _bitmapSource.SetBitmapAsync(bitmap);
                        }
                        finally
                        {
                            bitmap.Dispose();
                        }
                    }
                }
                catch { }
                finally
                {
                    _renderTaskRunning = false;
                }
            });
        }
        catch { }
    }

    private async Task StopCameraAsync(bool raiseStatus = true)
    {
        try
        {
            _isShuttingDown = true;

            var virtualView = VirtualView;
            var frameReader = _frameReader;
            _frameReader = null;

            if (frameReader != null)
            {
                frameReader.FrameArrived -= OnFrameArrived;
                try
                {
                    await frameReader.StopAsync();
                }
                catch { }
                frameReader.Dispose();
            }

            var mediaCapture = _mediaCapture;
            _mediaCapture = null;
            mediaCapture?.Dispose();

            Interlocked.Exchange(ref _backBuffer, null)?.Dispose();

            _bitmapSource = null;
            if (_previewImage != null)
                _previewImage.Source = null;
            _previewImage = null;

            _isRunning = false;

            if (raiseStatus && virtualView != null)
            {
                virtualView.IsRunning = false;
                virtualView.RaiseScanStatus("Camera stopped");
            }
        }
        catch { }
    }
}
