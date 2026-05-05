using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Codeland.QRScanner;

/// <summary>
/// Reusable QR scanner component that hosts camera preview, QR detection callbacks, and zoom controls.
/// </summary>
public partial class QRScanner : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    private readonly string _videoElementId = $"qrVideo_{Guid.NewGuid():N}";
    private readonly string _containerElementId = $"qrContainer_{Guid.NewGuid():N}";

    private DotNetObjectReference<QRScanner>? _dotRef;
    private bool _isRunning;

    private bool _zoomSupported;
    private double _zoomMin = 1;
    private double _zoomMax = 1;

    #region Parameters 

    /// <summary>
    /// Gets or sets a value indicating whether the scanner starts automatically after first render.
    /// </summary>
    [Parameter]
    public bool AutoStart { get; set; } = true;

    /// <summary>
    /// Gets or sets the last detected QR value.
    /// </summary>
    [Parameter]
    public string QRValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current camera zoom value.
    /// </summary>
    [Parameter]
    public double ZoomValue { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether pinch zoom is captured from the full window.
    /// When false, pinch zoom is captured from the scanner area only.
    /// </summary>
    [Parameter]
    public bool CaptureZoomFromFullScreen { get; set; }

    /// <summary>
    /// Gets or sets an optional HTML element id used to capture pinch-zoom gestures.
    /// When provided, it takes precedence over <see cref="CaptureZoomFromFullScreen"/>.
    /// </summary>
    [Parameter]
    public string? ZoomCaptureElementId { get; set; }

    /// <summary>
    /// When true (default) the scanner fills the entire viewport (position:fixed, 100vw x 100vh).
    /// Set to false to let the component size itself via <see cref="Class"/>, <see cref="Style"/>,
    /// <see cref="Width"/> and <see cref="Height"/>.
    /// </summary>
    [Parameter]
    public bool FullPage { get; set; } = true;

    /// <summary>Extra CSS class(es) applied to the outer container. Used when FullPage is false.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Inline style applied to the outer container. Used when FullPage is false.</summary>
    [Parameter]
    public string? Style { get; set; }

    /// <summary>CSS width of the container (e.g. "640px", "100%"). Ignored when FullPage is true.</summary>
    [Parameter]
    public string? Width { get; set; }

    /// <summary>CSS height of the container (e.g. "480px", "50vh"). Ignored when FullPage is true.</summary>
    [Parameter]
    public string? Height { get; set; }

    #endregion

    #region Events

    /// <summary>
    /// Gets or sets the callback invoked when zoom value changes.
    /// </summary>
    [Parameter]
    public EventCallback<double> OnZoomChanged { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when a QR value is detected.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnQRDetected { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked for scanner status messages.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnScanStatus { get; set; }

    #endregion

    #region Computed CSS helpers 

    private string _containerClass =>
        FullPage
            ? $"scanner-page{(string.IsNullOrWhiteSpace(Class) ? "" : " " + Class)}"
            : $"scanner-container{(string.IsNullOrWhiteSpace(Class) ? "" : " " + Class)}";

    private string _videoClass =>
        FullPage ? "scanner-video" : "scanner-video-fit";

    private string ContainerStyle
    {
        get
        {
            if (FullPage)
            {
                return Style ?? string.Empty;
            }

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Width))
            {
                parts.Add($"width:{Width}");
            }

            if (!string.IsNullOrWhiteSpace(Height))
            {
                parts.Add($"height:{Height}");
            }

            if (!string.IsNullOrWhiteSpace(Style))
            {
                parts.Add(Style.TrimEnd(';'));
            }

            return string.Join(";", parts);
        }
    }

    private string VideoStyle => string.Empty;

    #endregion

    #region Lifecycle 

    /// <summary>
    /// Starts the scanner on first render when <see cref="AutoStart"/> is enabled.
    /// </summary>
    /// <param name="firstRender">Indicates whether this is the first render.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !AutoStart)
        {
            return;
        }

        await Start();
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Starts scanner capture and QR detection.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Start()
    {
        if (_isRunning)
        {
            return;
        }

        _dotRef ??= DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("qrScanner.startAuto", _dotRef, _videoElementId, _containerElementId, CaptureZoomFromFullScreen, ZoomCaptureElementId);
        _isRunning = true;
    }

    /// <summary>
    /// Stops scanner capture and detection.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Stop()
    {
        await JS.InvokeVoidAsync("qrScanner.dispose");
        _isRunning = false;
    }

    /// <summary>
    /// Starts scanning if it is not already running.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task Scan()
    {
        return Start();
    }

    /// <summary>
    /// Applies the requested camera zoom value.
    /// </summary>
    /// <param name="zoomValue">The zoom value to apply.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Zoom(double zoomValue)
    {
        ZoomValue = zoomValue;
        await JS.InvokeVoidAsync("qrScanner.setZoom", zoomValue);
    }

    #endregion

    #region Private handlers 

    /// <summary>
    /// Handles zoom slider input and applies the selected value.
    /// </summary>
    /// <param name="e">Change event arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleZoomInput(ChangeEventArgs e)
    {
        if (!double.TryParse(e?.Value?.ToString(), out var zoom))
        {
            return;
        }

        await Zoom(zoom);
    }

    /// <summary>
    /// Receives detected QR values from JavaScript and forwards them to subscribers.
    /// </summary>
    /// <param name="value">Detected QR value.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [JSInvokable("OnQrDetected")]
    public async Task HandleQrDetected(string value)
    {
        QRValue = value;
        await OnQRDetected.InvokeAsync(value);
        StateHasChanged();
    }

    /// <summary>
    /// Receives zoom capability and current zoom updates from JavaScript.
    /// </summary>
    /// <param name="min">Minimum supported zoom.</param>
    /// <param name="max">Maximum supported zoom.</param>
    /// <param name="current">Current zoom value.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [JSInvokable("OnZoomCapabilities")]
    public async Task HandleZoomCapabilities(double min, double max, double current)
    {
        _zoomMin = min;
        _zoomMax = max;
        _zoomSupported = max > min;
        ZoomValue = current;

        await OnZoomChanged.InvokeAsync(current);
        StateHasChanged();
    }

    /// <summary>
    /// Receives scanner status messages from JavaScript.
    /// </summary>
    /// <param name="message">Status message.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [JSInvokable("OnScanStatus")]
    public Task HandleScanStatus(string message)
    {
        return OnScanStatus.InvokeAsync(message);
    }

    /// <summary>
    /// Disposes scanner resources and interop references.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Stop();
        }
        catch
        {
        }

        _dotRef?.Dispose();
    }
    #endregion
}
