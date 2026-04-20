using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Codeland.QRScanner;

namespace Codeland.ScannerQR.Pages;

public partial class Home
{
    private global::Codeland.QRScanner.QRScanner? _scannerRef;

    private bool _autoStart = true;
    private string _qrValue = string.Empty;
    private double _zoomValue = 1;
    private bool _showQrDialog;

    private string _eventQrDetected = string.Empty;
    private string _eventScanStatus = string.Empty;
    private double _eventZoomChanged = 1;

    private void CloseQrDialog()
    {
        _showQrDialog = false;
    }

    private async Task StartScanner()
    {
        if (_scannerRef is null)
        {
            return;
        }

        await _scannerRef.Start();
    }

    private async Task StopScanner()
    {
        if (_scannerRef is null)
        {
            return;
        }

        await _scannerRef.Stop();
    }

    private async Task ScanNow()
    {
        if (_scannerRef is null)
        {
            return;
        }

        await _scannerRef.Scan();
    }

    private async Task ZoomIn()
    {
        if (_scannerRef is null)
        {
            return;
        }

        _zoomValue += 0.2;
        await _scannerRef.Zoom(_zoomValue);
    }

    private async Task ZoomOut()
    {
        if (_scannerRef is null)
        {
            return;
        }

        _zoomValue = Math.Max(1, _zoomValue - 0.2);
        await _scannerRef.Zoom(_zoomValue);
    }

    private Task HandleQRDetected(string value)
    {
        _qrValue = value;
        _eventQrDetected = value;
        _showQrDialog = true;
        return Task.CompletedTask;
    }

    private Task HandleZoomChanged(double zoomValue)
    {
        _zoomValue = zoomValue;
        _eventZoomChanged = zoomValue;
        return Task.CompletedTask;
    }

    private Task HandleScanStatus(string message)
    {
        _eventScanStatus = message;
        return Task.CompletedTask;
    }
}
