using Microsoft.Maui.Handlers;
using Codeland.ScannerQR.Controls;

namespace Codeland.ScannerQR.Handlers;

/// <summary>
/// Shared command and property mapper for the cross-platform <see cref="QRScannerView"/> handler.
/// Platform-specific camera implementations are provided in partial handler classes.
/// </summary>
public partial class QRScannerHandler
{
    /// <summary>
    /// Gets the property mapper used by the QR scanner handler.
    /// </summary>
    public static IPropertyMapper<QRScannerView, QRScannerHandler> PropertyMapper =
        new PropertyMapper<QRScannerView, QRScannerHandler>(ViewHandler.ViewMapper)
        {
        };

    /// <summary>
    /// Gets the command mapper used by the QR scanner handler.
    /// </summary>
    public static CommandMapper<QRScannerView, QRScannerHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper)
        {
            [nameof(QRScannerView.StartScanning)] = (handler, view, args) => handler.StartCamera(),
            [nameof(QRScannerView.StopScanning)] = (handler, view, args) => handler.StopCamera(),
            [nameof(QRScannerView.ApplyZoom)] = (handler, view, args) =>
            {
                if (args is double zoom)
                    handler.SetZoom(zoom);
            },
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="QRScannerHandler"/> class.
    /// </summary>
    public QRScannerHandler() : base(PropertyMapper, CommandMapper)
    {
    }

    /// <summary>
    /// Starts the platform camera implementation.
    /// </summary>
    partial void StartCamera();

    /// <summary>
    /// Stops the platform camera implementation.
    /// </summary>
    partial void StopCamera();

    /// <summary>
    /// Applies the requested zoom level using the platform camera implementation.
    /// </summary>
    /// <param name="zoom">The requested zoom level.</param>
    partial void SetZoom(double zoom);
}
