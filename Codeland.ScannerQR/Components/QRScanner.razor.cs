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

    [Parameter]
    public bool AutoStart { get; set; } = true;

    [Parameter]
    public string QRValue { get; set; } = string.Empty;

    [Parameter]
    public double ZoomValue { get; set; } = 1;

    [Parameter]
    public EventCallback<double> OnZoomChanged { get; set; }

    [Parameter]
    public EventCallback<string> OnQRDetected { get; set; }

    [Parameter]
    public EventCallback<string> OnScanStatus { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !AutoStart)
        {
            return;
        }

        await Start();
    }

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
