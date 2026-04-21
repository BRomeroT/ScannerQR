namespace Codeland.ScannerQR.Controls;

/// <summary>
/// Cross-platform QR scanner view that uses the device camera to detect QR codes.
/// Supports zoom control and provides scan events.
/// </summary>
public class QRScannerView : View
{
    #region Bindable Properties

    public static readonly BindableProperty AutoStartProperty =
        BindableProperty.Create(nameof(AutoStart), typeof(bool), typeof(QRScannerView), true);

    public static readonly BindableProperty ZoomValueProperty =
        BindableProperty.Create(nameof(ZoomValue), typeof(double), typeof(QRScannerView), 1.0,
            propertyChanged: (b, o, n) => ((QRScannerView)b).Handler?.Invoke(nameof(ApplyZoom), n));

    public static readonly BindableProperty IsRunningProperty =
        BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(QRScannerView), false);

    /// <summary>Starts the camera automatically on first render.</summary>
    public bool AutoStart
    {
        get => (bool)GetValue(AutoStartProperty);
        set => SetValue(AutoStartProperty, value);
    }

    /// <summary>Current camera zoom level.</summary>
    public double ZoomValue
    {
        get => (double)GetValue(ZoomValueProperty);
        set => SetValue(ZoomValueProperty, value);
    }

    /// <summary>Whether the scanner is currently running.</summary>
    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    #endregion

    #region Events

    /// <summary>Fired when a QR code is detected.</summary>
    public event EventHandler<string>? QRDetected;

    /// <summary>Fired when the zoom level changes.</summary>
    public event EventHandler<double>? ZoomChanged;

    /// <summary>Fired with lifecycle and status messages.</summary>
    public event EventHandler<string>? ScanStatusChanged;

    #endregion

    #region Internal event raisers (called by handlers)

    private DateTime _lastDetection = DateTime.MinValue;
    private string _lastQRValue = string.Empty;

    internal void RaiseQRDetected(string value)
    {
        // Duplicate suppression (~1.2s)
        var now = DateTime.UtcNow;
        if (value == _lastQRValue && (now - _lastDetection).TotalMilliseconds < 1200)
            return;

        _lastQRValue = value;
        _lastDetection = now;

        MainThread.BeginInvokeOnMainThread(() => QRDetected?.Invoke(this, value));
    }

    internal void RaiseZoomChanged(double zoom)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetValue(ZoomValueProperty, zoom);
            ZoomChanged?.Invoke(this, zoom);
        });
    }

    internal void RaiseScanStatus(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => ScanStatusChanged?.Invoke(this, message));
    }

    #endregion

    #region Public methods (command mapped to handlers)

    /// <summary>Opens the device camera and starts QR detection.</summary>
    public void StartScanning() => Handler?.Invoke(nameof(StartScanning));

    /// <summary>Stops the camera and releases resources.</summary>
    public void StopScanning() => Handler?.Invoke(nameof(StopScanning));

    /// <summary>Sets the camera zoom level.</summary>
    public void ApplyZoom(double zoom)
    {
        ZoomValue = zoom;
    }

    /// <summary>Sets the camera zoom level (convenience alias).</summary>
    public void Zoom(double zoomValue)
    {
        ZoomValue = zoomValue;
    }

    #endregion
}
