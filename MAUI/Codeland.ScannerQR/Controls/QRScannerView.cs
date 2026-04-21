namespace Codeland.ScannerQR.Controls;

/// <summary>
/// Cross-platform QR scanner view that uses the device camera to detect QR codes.
/// Supports zoom control and provides scan events.
/// </summary>
public class QRScannerView : View
{
    #region Bindable Properties

    /// <summary>
    /// Identifies the <see cref="AutoStart"/> bindable property.
    /// </summary>
    public static readonly BindableProperty AutoStartProperty =
        BindableProperty.Create(nameof(AutoStart), typeof(bool), typeof(QRScannerView), true);

    /// <summary>
    /// Identifies the <see cref="ZoomValue"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ZoomValueProperty =
        BindableProperty.Create(nameof(ZoomValue), typeof(double), typeof(QRScannerView), 1.0,
            propertyChanged: (b, o, n) => ((QRScannerView)b).Handler?.Invoke(nameof(ApplyZoom), n));

    /// <summary>
    /// Identifies the <see cref="IsRunning"/> bindable property.
    /// </summary>
    public static readonly BindableProperty IsRunningProperty =
        BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(QRScannerView), false);

    /// <summary>
    /// Gets or sets a value indicating whether the camera starts automatically when the view is first rendered.
    /// </summary>
    public bool AutoStart
    {
        get => (bool)GetValue(AutoStartProperty);
        set => SetValue(AutoStartProperty, value);
    }

    /// <summary>
    /// Gets or sets the current camera zoom level.
    /// </summary>
    public double ZoomValue
    {
        get => (double)GetValue(ZoomValueProperty);
        set => SetValue(ZoomValueProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the scanner is currently running.
    /// </summary>
    public bool IsRunning
    {
        get => (bool)GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a QR code is detected.
    /// </summary>
    public event EventHandler<string>? QRDetected;

    /// <summary>
    /// Occurs when the zoom level changes.
    /// </summary>
    public event EventHandler<double>? ZoomChanged;

    /// <summary>
    /// Occurs when scanner lifecycle or status messages change.
    /// </summary>
    public event EventHandler<string>? ScanStatusChanged;

    #endregion

    #region Internal event raisers (called by handlers)

    private DateTime _lastDetection = DateTime.MinValue;
    private string _lastQRValue = string.Empty;

    /// <summary>
    /// Raises the <see cref="QRDetected"/> event while suppressing duplicate values for a short interval.
    /// </summary>
    /// <param name="value">The decoded QR value.</param>
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

    /// <summary>
    /// Raises the <see cref="ZoomChanged"/> event and updates the bindable zoom value.
    /// </summary>
    /// <param name="zoom">The new zoom value.</param>
    internal void RaiseZoomChanged(double zoom)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetValue(ZoomValueProperty, zoom);
            ZoomChanged?.Invoke(this, zoom);
        });
    }

    /// <summary>
    /// Raises the <see cref="ScanStatusChanged"/> event.
    /// </summary>
    /// <param name="message">The status message to report.</param>
    internal void RaiseScanStatus(string message)
    {
        MainThread.BeginInvokeOnMainThread(() => ScanStatusChanged?.Invoke(this, message));
    }

    #endregion

    #region Public methods (command mapped to handlers)

    /// <summary>
    /// Opens the device camera and starts QR detection.
    /// </summary>
    public void StartScanning() => Handler?.Invoke(nameof(StartScanning));

    /// <summary>
    /// Stops the camera and releases resources.
    /// </summary>
    public void StopScanning() => Handler?.Invoke(nameof(StopScanning));

    /// <summary>
    /// Sets the camera zoom level by forwarding the value to the active platform handler.
    /// </summary>
    /// <param name="zoom">The requested zoom level.</param>
    public void ApplyZoom(double zoom)
    {
        ZoomValue = zoom;
    }

    /// <summary>
    /// Sets the camera zoom level.
    /// </summary>
    /// <param name="zoomValue">The requested zoom level.</param>
    public void Zoom(double zoomValue)
    {
        ZoomValue = zoomValue;
    }

    #endregion
}
