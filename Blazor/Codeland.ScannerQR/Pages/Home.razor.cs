using Codeland.QRScanner;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Codeland.ScannerQR.Pages;

/// <summary>
/// Home page that hosts the QR scanner and displays scanner state and events.
/// </summary>
public partial class Home
{
    private global::Codeland.QRScanner.QRScanner? _scannerRef;

    private bool _autoStart = true;
    private string _qrValue = string.Empty;
    private double _zoomValue = 1;
    private bool _showQrDialog;
    private bool _showDetectionCue;

    private string _eventQrDetected = string.Empty;
    private string _eventScanStatus = string.Empty;
    private double _eventZoomChanged = 1;

    /// <summary>
    /// Hides the QR detection dialog.
    /// </summary>
    private void CloseQrDialog()
    {
        _showQrDialog = false;
    }

    /// <summary>
    /// Starts scanner capture.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task StartScanner()
    {
        if (_scannerRef is null)
        {
            return;
        }

        await _scannerRef.Start();
    }

    /// <summary>
    /// Stops scanner capture.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task StopScanner()
    {
        if (_scannerRef is null)
        {
            return;
        }

        await _scannerRef.Stop();
    }

    /// <summary>
    /// Requests scanner start when triggered manually.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ScanNow()
    {
        if (_scannerRef is null)
        {
            return;
        }

        await _scannerRef.Scan();
    }

    /// <summary>
    /// Increases scanner zoom level.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ZoomIn()
    {
        if (_scannerRef is null)
        {
            return;
        }

        _zoomValue += 0.2;
        await _scannerRef.Zoom(_zoomValue);
    }

    /// <summary>
    /// Decreases scanner zoom level.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ZoomOut()
    {
        if (_scannerRef is null)
        {
            return;
        }

        _zoomValue = Math.Max(1, _zoomValue - 0.2);
        await _scannerRef.Zoom(_zoomValue);
    }

    /// <summary>
    /// Handles QR detection events and displays feedback cues.
    /// </summary>
    /// <param name="value">Detected QR value.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task HandleQRDetected(string value)
    {
        _qrValue = value;
        _eventQrDetected = value;
        _showQrDialog = true;
        await ShowDetectionCueAsync();
    }

    /// <summary>
    /// Handles zoom change events.
    /// </summary>
    /// <param name="zoomValue">Current zoom value.</param>
    /// <returns>A completed task.</returns>
    private Task HandleZoomChanged(double zoomValue)
    {
        _zoomValue = zoomValue;
        _eventZoomChanged = zoomValue;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles scanner status events.
    /// </summary>
    /// <param name="message">Status message.</param>
    /// <returns>A completed task.</returns>
    private Task HandleScanStatus(string message)
    {
        _eventScanStatus = message;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Displays a short visual cue after detection.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task ShowDetectionCueAsync()
    {
        _showDetectionCue = true;
        await InvokeAsync(StateHasChanged);

        await Task.Delay(220);

        _showDetectionCue = false;
        await InvokeAsync(StateHasChanged);
    }
}
