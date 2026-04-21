using Android.Content;
using Android.Graphics;
using Android.Views;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Codeland.ScannerQR.Controls;
using Java.Util.Concurrent;
using Microsoft.Maui.Handlers;
using ZXing;
using ZXing.Common;
using IZoomState = AndroidX.Camera.Core.IZoomState;

namespace Codeland.ScannerQR.Handlers;

/// <summary>
/// Android implementation of <see cref="QRScannerView"/> using CameraX for preview, analysis, and zoom control.
/// </summary>
public partial class QRScannerHandler : ViewHandler<QRScannerView, PreviewView>
{
    private ProcessCameraProvider? _cameraProvider;
    private ICamera? _camera;
    private IExecutorService? _analysisExecutor;
    private ScaleGestureDetector? _scaleGestureDetector;
    private bool _isRunning;

    /// <summary>
    /// Creates the native Android preview view.
    /// </summary>
    /// <returns>The configured <see cref="PreviewView"/>.</returns>
    protected override PreviewView CreatePlatformView()
    {
        var previewView = new PreviewView(Context)
        {
            LayoutParameters = new Android.Views.ViewGroup.LayoutParams(
                Android.Views.ViewGroup.LayoutParams.MatchParent,
                Android.Views.ViewGroup.LayoutParams.MatchParent)
        };
        previewView.SetImplementationMode(PreviewView.ImplementationMode.Compatible);
        return previewView;
    }

    /// <summary>
    /// Connects the MAUI view to its Android platform view and configures gesture and analysis resources.
    /// </summary>
    /// <param name="platformView">The native preview view.</param>
    protected override void ConnectHandler(PreviewView platformView)
    {
        base.ConnectHandler(platformView);
        _analysisExecutor = Executors.NewSingleThreadExecutor();

        _scaleGestureDetector = new ScaleGestureDetector(Context, new PinchZoomListener(this));
        platformView.SetOnTouchListener(new ZoomTouchListener(_scaleGestureDetector));

        if (VirtualView.AutoStart)
            StartCamera();
    }

    /// <summary>
    /// Disconnects the handler and releases Android resources.
    /// </summary>
    /// <param name="platformView">The native preview view.</param>
    protected override void DisconnectHandler(PreviewView platformView)
    {
        StopCamera();
        _analysisExecutor?.Shutdown();
        _analysisExecutor = null;
        base.DisconnectHandler(platformView);
    }

    /// <summary>
    /// Starts the Android camera if it is not already running.
    /// </summary>
    partial void StartCamera()
    {
        if (_isRunning) return;
        _ = StartCameraWithPermissionAsync();
    }

    /// <summary>
    /// Requests camera permission when needed and starts the CameraX pipeline.
    /// </summary>
    /// <returns>A task that completes when startup has been initiated.</returns>
    private async Task StartCameraWithPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                VirtualView.RaiseScanStatus("Camera permission denied");
                return;
            }
        }

        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            VirtualView.RaiseScanStatus("No activity available");
            return;
        }

        VirtualView.RaiseScanStatus("Starting camera...");

        var cameraProviderFuture = ProcessCameraProvider.GetInstance(activity);
        cameraProviderFuture.AddListener(new Java.Lang.Runnable(() =>
        {
            try
            {
                _cameraProvider = (ProcessCameraProvider)cameraProviderFuture.Get()!;
                BindCamera(activity);
                _isRunning = true;
                VirtualView.IsRunning = true;
                VirtualView.RaiseScanStatus("Camera started");
            }
            catch (Exception ex)
            {
                VirtualView.RaiseScanStatus($"Camera error: {ex.Message}");
            }
        }), ContextCompat.GetMainExecutor(activity));
    }

    /// <summary>
    /// Binds preview and image-analysis use cases to the current Android lifecycle owner.
    /// </summary>
    /// <param name="activity">The current Android activity.</param>
    private void BindCamera(Android.App.Activity activity)
    {
        _cameraProvider?.UnbindAll();

        var preview = new Preview.Builder()
            .SetTargetResolution(new Android.Util.Size(1920, 1080))
            .Build();

        preview.SetSurfaceProvider(ContextCompat.GetMainExecutor(activity), PlatformView.SurfaceProvider);

        var imageAnalysis = new ImageAnalysis.Builder()
            .SetTargetResolution(new Android.Util.Size(1280, 720))
            .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
            .Build();

        imageAnalysis.SetAnalyzer(_analysisExecutor!, new QRImageAnalyzer(VirtualView));

        var cameraSelector = new CameraSelector.Builder()
            .RequireLensFacing(CameraSelector.LensFacingBack)
            .Build();

        var lifecycleOwner = activity as ILifecycleOwner;
        if (lifecycleOwner == null)
        {
            VirtualView.RaiseScanStatus("Activity is not a LifecycleOwner");
            return;
        }

        _camera = _cameraProvider!.BindToLifecycle(
            lifecycleOwner, cameraSelector, preview, imageAnalysis);

        // Report zoom capabilities
        if (_camera.CameraInfo.ZoomState.Value is IZoomState zs)
        {
            VirtualView.RaiseZoomChanged(zs.ZoomRatio);
        }
    }

    /// <summary>
    /// Stops the Android camera and releases bound CameraX use cases.
    /// </summary>
    partial void StopCamera()
    {
        _cameraProvider?.UnbindAll();
        _camera = null;
        _isRunning = false;
        VirtualView.IsRunning = false;
        VirtualView.RaiseScanStatus("Camera stopped");
    }

    /// <summary>
    /// Applies a zoom value to the active Android camera.
    /// </summary>
    /// <param name="zoom">The requested zoom level.</param>
    partial void SetZoom(double zoom)
    {
        if (_camera == null) return;

        if (_camera.CameraInfo.ZoomState.Value is not IZoomState zoomState) return;

        var clamped = Math.Clamp((float)zoom, zoomState.MinZoomRatio, zoomState.MaxZoomRatio);
        _camera.CameraControl.SetZoomRatio(clamped);
        VirtualView.RaiseZoomChanged(clamped);
    }

    /// <summary>
    /// CameraX analyzer that reads the luminance plane and decodes QR codes using ZXing.
    /// </summary>
    private class QRImageAnalyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
    {
        private readonly QRScannerView _view;
        private readonly ZXing.BarcodeReaderGeneric _reader;

        /// <summary>
        /// Initializes a new instance of the <see cref="QRImageAnalyzer"/> class.
        /// </summary>
        /// <param name="view">The scanner view that receives decoded results.</param>
        public QRImageAnalyzer(QRScannerView view)
        {
            _view = view;
            _reader = new ZXing.BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    PossibleFormats = [BarcodeFormat.QR_CODE],
                    TryHarder = true,
                    TryInverted = true
                }
            };
        }

        /// <summary>
        /// Gets the preferred analysis resolution for CameraX.
        /// </summary>
        public Android.Util.Size DefaultTargetResolution => new Android.Util.Size(1280, 720);

        /// <summary>
        /// Analyzes a camera frame and attempts to decode a QR code.
        /// </summary>
        /// <param name="imageProxy">The current CameraX image frame.</param>
        public void Analyze(IImageProxy imageProxy)
        {
            try
            {
                var plane = imageProxy.GetPlanes()[0];
                var buffer = plane.Buffer!;
                buffer.Rewind();

                var bytes = new byte[buffer.Remaining()];
                buffer.Get(bytes);

                var width = imageProxy.Width;
                var height = imageProxy.Height;
                var rowStride = plane.RowStride;

                byte[] luminance;
                if (rowStride == width)
                {
                    luminance = bytes;
                }
                else
                {
                    luminance = new byte[width * height];
                    for (var y = 0; y < height; y++)
                    {
                        Buffer.BlockCopy(bytes, y * rowStride, luminance, y * width, width);
                    }
                }

                var source = new ZXing.PlanarYUVLuminanceSource(
                    luminance, width, height, 0, 0, width, height, false);

                var result = _reader.Decode(source);
                if (result != null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    _view.RaiseQRDetected(result.Text);
                }
            }
            catch
            {
            }
            finally
            {
                imageProxy.Close();
            }
        }
    }

    /// <summary>
    /// Handles Android pinch gestures and translates them into camera zoom changes.
    /// </summary>
    private class PinchZoomListener : ScaleGestureDetector.SimpleOnScaleGestureListener
    {
        private readonly QRScannerHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="PinchZoomListener"/> class.
        /// </summary>
        /// <param name="handler">The parent scanner handler.</param>
        public PinchZoomListener(QRScannerHandler handler) => _handler = handler;

        /// <summary>
        /// Applies incremental zoom during a pinch gesture.
        /// </summary>
        /// <param name="detector">The active scale gesture detector.</param>
        /// <returns><see langword="true"/> when the gesture is handled.</returns>
        public override bool OnScale(ScaleGestureDetector detector)
        {
            if (_handler._camera?.CameraInfo.ZoomState.Value is not IZoomState zoomState)
                return true;

            var newZoom = zoomState.ZoomRatio * detector.ScaleFactor;
            var clamped = Math.Clamp(newZoom, zoomState.MinZoomRatio, zoomState.MaxZoomRatio);
            _handler._camera.CameraControl.SetZoomRatio(clamped);
            _handler.VirtualView.RaiseZoomChanged(clamped);
            return true;
        }
    }

    /// <summary>
    /// Forwards Android touch events to the scale gesture detector.
    /// </summary>
    private class ZoomTouchListener : Java.Lang.Object, Android.Views.View.IOnTouchListener
    {
        private readonly ScaleGestureDetector _detector;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZoomTouchListener"/> class.
        /// </summary>
        /// <param name="detector">The scale gesture detector to notify.</param>
        public ZoomTouchListener(ScaleGestureDetector detector) => _detector = detector;

        /// <summary>
        /// Handles Android touch input for pinch zoom.
        /// </summary>
        /// <param name="v">The touched view.</param>
        /// <param name="e">The motion event.</param>
        /// <returns><see langword="true"/> when the touch event is consumed.</returns>
        public bool OnTouch(Android.Views.View? v, MotionEvent? e)
        {
            if (e != null) _detector.OnTouchEvent(e);
            return true;
        }
    }
}
