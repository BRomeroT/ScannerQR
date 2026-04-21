using AVFoundation;
using Codeland.ScannerQR.Controls;
using CoreFoundation;
using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace Codeland.ScannerQR.Handlers;

/// <summary>
/// iOS implementation of <see cref="QRScannerView"/> using AVFoundation preview, metadata detection, and zoom.
/// </summary>
public partial class QRScannerHandler : ViewHandler<QRScannerView, UIView>
{
    private AVCaptureSession? _captureSession;
    private AVCaptureDevice? _captureDevice;
    private AVCaptureVideoPreviewLayer? _previewLayer;
    private AVCaptureMetadataOutput? _metadataOutput;
    private QRMetadataOutputDelegate? _metadataDelegate;
    private LayoutObserver? _layoutObserver;
    private UIPinchGestureRecognizer? _pinchGestureRecognizer;
    private nfloat _pinchStartZoom = 1f;
    private bool _isRunning;
    private bool _isShuttingDown;

    /// <summary>
    /// Creates the native iOS preview surface.
    /// </summary>
    /// <returns>The configured <see cref="UIView"/>.</returns>
    protected override UIView CreatePlatformView()
    {
        return new UIView
        {
            BackgroundColor = UIColor.Black,
            ClipsToBounds = true,
            MultipleTouchEnabled = true
        };
    }

    /// <summary>
    /// Connects the handler, attaches gestures, and optionally starts the camera.
    /// </summary>
    /// <param name="platformView">The native iOS view.</param>
    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);
        _isShuttingDown = false;

        _pinchGestureRecognizer = new UIPinchGestureRecognizer(HandlePinchGesture);
        platformView.AddGestureRecognizer(_pinchGestureRecognizer);

        if (VirtualView.AutoStart)
            StartCamera();
    }

    /// <summary>
    /// Disconnects the handler and releases iOS camera resources.
    /// </summary>
    /// <param name="platformView">The native iOS view.</param>
    protected override void DisconnectHandler(UIView platformView)
    {
        _isShuttingDown = true;
        StopCameraInternal(raiseStatus: false);

        if (_pinchGestureRecognizer != null)
        {
            platformView.RemoveGestureRecognizer(_pinchGestureRecognizer);
            _pinchGestureRecognizer.Dispose();
            _pinchGestureRecognizer = null;
        }

        base.DisconnectHandler(platformView);
    }

    /// <summary>
    /// Starts the iOS camera if it is not already running.
    /// </summary>
    partial void StartCamera()
    {
        if (_isRunning || _isShuttingDown)
            return;

        _ = StartCameraWithPermissionAsync();
    }

    /// <summary>
    /// Requests camera permission and initializes the AVFoundation capture pipeline.
    /// </summary>
    /// <returns>A task that completes when camera startup has been initiated.</returns>
    private async Task StartCameraWithPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                VirtualView?.RaiseScanStatus("Camera permission denied");
                return;
            }
        }

        if (_isShuttingDown || VirtualView == null)
            return;

        VirtualView.RaiseScanStatus("Starting camera...");

        _captureSession = new AVCaptureSession();
        if (_captureSession.CanSetSessionPreset(AVCaptureSession.Preset1920x1080))
            _captureSession.SessionPreset = AVCaptureSession.Preset1920x1080;

        _captureDevice = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
        if (_captureDevice == null)
        {
            VirtualView.RaiseScanStatus("No camera available");
            return;
        }

        NSError? error;
        var input = new AVCaptureDeviceInput(_captureDevice, out error);
        if (error != null || input == null)
        {
            VirtualView.RaiseScanStatus($"Camera error: {error?.LocalizedDescription ?? "Unable to create camera input"}");
            return;
        }

        if (!_captureSession.CanAddInput(input))
        {
            VirtualView.RaiseScanStatus("Unable to attach camera input");
            return;
        }

        _captureSession.AddInput(input);

        _metadataOutput = new AVCaptureMetadataOutput();
        if (!_captureSession.CanAddOutput(_metadataOutput))
        {
            VirtualView.RaiseScanStatus("Unable to attach QR metadata output");
            return;
        }

        _captureSession.AddOutput(_metadataOutput);
        _metadataDelegate = new QRMetadataOutputDelegate(this);
        _metadataOutput.SetDelegate(_metadataDelegate, DispatchQueue.MainQueue);
        _metadataOutput.MetadataObjectTypes = AVMetadataObjectType.QRCode;

        _previewLayer = new AVCaptureVideoPreviewLayer(_captureSession)
        {
            VideoGravity = AVLayerVideoGravity.ResizeAspectFill,
            Frame = PlatformView.Bounds
        };
        PlatformView.Layer.AddSublayer(_previewLayer);

        _layoutObserver = new LayoutObserver(_previewLayer);
        PlatformView.AddObserver(_layoutObserver, "bounds", NSKeyValueObservingOptions.New, nint.Zero);

        DispatchQueue.GetGlobalQueue(DispatchQueuePriority.Default).DispatchAsync(() =>
        {
            try
            {
                _captureSession?.StartRunning();
            }
            catch
            {
            }
        });

        _isRunning = true;
        VirtualView.IsRunning = true;
        VirtualView.RaiseScanStatus("Camera started");
        VirtualView.RaiseZoomChanged(_captureDevice.VideoZoomFactor);
    }

    /// <summary>
    /// Stops the iOS camera pipeline.
    /// </summary>
    partial void StopCamera()
    {
        StopCameraInternal();
    }

    /// <summary>
    /// Releases AVFoundation resources and optionally raises a stop status message.
    /// </summary>
    /// <param name="raiseStatus"><see langword="true"/> to raise a stop status message; otherwise, <see langword="false"/>.</param>
    private void StopCameraInternal(bool raiseStatus = true)
    {
        try
        {
            _isShuttingDown = true;
            var virtualView = VirtualView;

            if (_layoutObserver != null)
            {
                try
                {
                    PlatformView?.RemoveObserver(_layoutObserver, "bounds");
                }
                catch
                {
                }

                _layoutObserver.Dispose();
                _layoutObserver = null;
            }

            _metadataOutput = null;
            _metadataDelegate = null;

            var previewLayer = _previewLayer;
            _previewLayer = null;
            previewLayer?.RemoveFromSuperLayer();

            var captureSession = _captureSession;
            _captureSession = null;

            if (captureSession != null)
            {
                DispatchQueue.GetGlobalQueue(DispatchQueuePriority.Default).DispatchAsync(() =>
                {
                    try
                    {
                        if (captureSession.Running)
                            captureSession.StopRunning();
                    }
                    catch
                    {
                    }
                    finally
                    {
                        captureSession.Dispose();
                    }
                });
            }

            _captureDevice = null;
            _isRunning = false;

            if (raiseStatus && virtualView != null)
            {
                virtualView.IsRunning = false;
                virtualView.RaiseScanStatus("Camera stopped");
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Applies zoom to the active iOS capture device.
    /// </summary>
    /// <param name="zoom">The requested zoom value.</param>
    partial void SetZoom(double zoom)
    {
        if (_isShuttingDown || _captureDevice == null)
            return;

        var minZoom = Math.Max(1d, _captureDevice.MinAvailableVideoZoomFactor);
        var maxZoom = Math.Max(minZoom, _captureDevice.MaxAvailableVideoZoomFactor);
        var clamped = Math.Clamp(zoom, minZoom, maxZoom);

        NSError? error;
        if (_captureDevice.LockForConfiguration(out error))
        {
            try
            {
                _captureDevice.VideoZoomFactor = (nfloat)clamped;
                VirtualView?.RaiseZoomChanged(clamped);
            }
            finally
            {
                _captureDevice.UnlockForConfiguration();
            }
        }
    }

    /// <summary>
    /// Handles pinch gestures and converts them into camera zoom changes.
    /// </summary>
    /// <param name="recognizer">The active pinch recognizer.</param>
    private void HandlePinchGesture(UIPinchGestureRecognizer recognizer)
    {
        if (_isShuttingDown || _captureDevice == null)
            return;

        if (recognizer.State == UIGestureRecognizerState.Began)
        {
            _pinchStartZoom = _captureDevice.VideoZoomFactor;
            return;
        }

        if (recognizer.State is UIGestureRecognizerState.Changed or UIGestureRecognizerState.Ended)
        {
            SetZoom(_pinchStartZoom * recognizer.Scale);
        }
    }

    /// <summary>
    /// Receives native AVFoundation metadata callbacks and forwards QR values to the MAUI scanner view.
    /// </summary>
    private class QRMetadataOutputDelegate : AVCaptureMetadataOutputObjectsDelegate
    {
        private readonly QRScannerHandler _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="QRMetadataOutputDelegate"/> class.
        /// </summary>
        /// <param name="handler">The owning scanner handler.</param>
        public QRMetadataOutputDelegate(QRScannerHandler handler) => _handler = handler;

        /// <summary>
        /// Processes detected metadata objects and emits the first QR value found.
        /// </summary>
        /// <param name="captureOutput">The metadata output that produced the callback.</param>
        /// <param name="metadataObjects">The detected metadata objects.</param>
        /// <param name="connection">The capture connection.</param>
        public override void DidOutputMetadataObjects(AVCaptureMetadataOutput captureOutput,
            AVMetadataObject[] metadataObjects, AVCaptureConnection connection)
        {
            if (_handler._isShuttingDown)
                return;

            foreach (var metadata in metadataObjects)
            {
                if (metadata is AVMetadataMachineReadableCodeObject qr && !string.IsNullOrWhiteSpace(qr.StringValue))
                {
                    _handler.VirtualView?.RaiseQRDetected(qr.StringValue);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Observes layout changes and resizes the native preview layer to match the view bounds.
    /// </summary>
    private class LayoutObserver : NSObject
    {
        private readonly AVCaptureVideoPreviewLayer _layer;

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutObserver"/> class.
        /// </summary>
        /// <param name="layer">The preview layer to resize.</param>
        public LayoutObserver(AVCaptureVideoPreviewLayer layer) => _layer = layer;

        /// <summary>
        /// Updates the preview layer frame when the observed view bounds change.
        /// </summary>
        /// <param name="keyPath">The observed key path.</param>
        /// <param name="ofObject">The observed object.</param>
        /// <param name="change">The change payload.</param>
        /// <param name="context">The observer context.</param>
        public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary change, nint context)
        {
            if (ofObject is UIView view)
                _layer.Frame = view.Bounds;
        }
    }
}
