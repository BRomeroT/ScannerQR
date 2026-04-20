using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Codeland.QRScanner;

public partial class QRScanner : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    private readonly string _videoElementId = $"qrVideo_{Guid.NewGuid():N}";

    private DotNetObjectReference<QRScanner>? _dotRef;
    private bool _isRunning;

    private bool _zoomSupported;
    private double _zoomMin = 1;
    private double _zoomMax = 1;

    // ── Parameters ──────────────────────────────────────────────────────────

    [Parameter]
    public bool AutoStart { get; set; } = true;

    [Parameter]
    public string QRValue { get; set; } = string.Empty;

    [Parameter]
    public double ZoomValue { get; set; } = 1;

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

    // ── Events ───────────────────────────────────────────────────────────────

    [Parameter]
    public EventCallback<double> OnZoomChanged { get; set; }

    [Parameter]
    public EventCallback<string> OnQRDetected { get; set; }

    [Parameter]
    public EventCallback<string> OnScanStatus { get; set; }

    // ── Computed CSS helpers ─────────────────────────────────────────────────

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

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !AutoStart)
        {
            return;
        }

        await Start();
    }

    // ── Public methods ───────────────────────────────────────────────────────

    public async Task Start()
    {
        if (_isRunning)
        {
            return;
        }

        _dotRef ??= DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("qrScanner.startAuto", _dotRef, _videoElementId);
        _isRunning = true;
    }

    public async Task Stop()
    {
        await JS.InvokeVoidAsync("qrScanner.dispose");
        _isRunning = false;
    }

    public Task Scan()
    {
        return Start();
    }

    public async Task Zoom(double zoomValue)
    {
        ZoomValue = zoomValue;
        await JS.InvokeVoidAsync("qrScanner.setZoom", zoomValue);
    }

    // ── Private handlers ─────────────────────────────────────────────────────

    private async Task HandleZoomInput(ChangeEventArgs e)
    {
        if (!double.TryParse(e?.Value?.ToString(), out var zoom))
        {
            return;
        }

        await Zoom(zoom);
    }

    [JSInvokable("OnQrDetected")]
    public async Task HandleQrDetected(string value)
    {
        QRValue = value;
        await OnQRDetected.InvokeAsync(value);
        StateHasChanged();
    }

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

    [JSInvokable("OnScanStatus")]
    public Task HandleScanStatus(string message)
    {
        return OnScanStatus.InvokeAsync(message);
    }

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
}
