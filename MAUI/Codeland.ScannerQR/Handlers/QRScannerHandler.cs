using Microsoft.Maui.Handlers;
using Codeland.ScannerQR.Controls;

namespace Codeland.ScannerQR.Handlers;

public partial class QRScannerHandler
{
    public static IPropertyMapper<QRScannerView, QRScannerHandler> PropertyMapper =
        new PropertyMapper<QRScannerView, QRScannerHandler>(ViewHandler.ViewMapper)
        {
        };

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

    public QRScannerHandler() : base(PropertyMapper, CommandMapper)
    {
    }

    partial void StartCamera();
    partial void StopCamera();
    partial void SetZoom(double zoom);
}
