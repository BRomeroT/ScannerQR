namespace Codeland.ScannerQR
{
    public partial class MainPage : ContentPage
    {
        private readonly bool _autoStart = true;
        private string _qrValue = string.Empty;
        private double _zoomValue = 1.0;

        private string _eventQrDetected = string.Empty;
        private double _eventZoomChanged = 1.0;
        private string _eventScanStatus = string.Empty;

        public MainPage()
        {
            InitializeComponent();
            RefreshInfoLabels();
        }

        private void OnStartClicked(object? sender, EventArgs e)
        {
            Scanner.StartScanning();
        }

        private void OnStopClicked(object? sender, EventArgs e)
        {
            Scanner.StopScanning();
        }

        private void OnScanClicked(object? sender, EventArgs e)
        {
            Scanner.StartScanning();
        }

        private void OnZoomInClicked(object? sender, EventArgs e)
        {
            _zoomValue = Math.Min(_zoomValue + 0.2, 10);
            Scanner.Zoom(_zoomValue);
        }

        private void OnZoomOutClicked(object? sender, EventArgs e)
        {
            _zoomValue = Math.Max(_zoomValue - 0.2, 1);
            Scanner.Zoom(_zoomValue);
        }

        private async void OnQRDetected(object? sender, string value)
        {
            _qrValue = value;
            _eventQrDetected = value;

            ScanFrame.Stroke = Colors.LimeGreen;
            ScanFrame.StrokeThickness = 4;

            await FlashOverlay.FadeTo(1, 100);
            await FlashOverlay.FadeTo(0, 300);

            _ = Task.Delay(1200).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
            {
                ScanFrame.Stroke = Color.FromArgb("#80FFFFFF");
                ScanFrame.StrokeThickness = 2;
            }));

            DialogQrValueLabel.Text = value;
            QrDialogOverlay.IsVisible = true;

            RefreshInfoLabels();
        }

        private void OnZoomChanged(object? sender, double zoom)
        {
            _zoomValue = zoom;
            _eventZoomChanged = zoom;
            RefreshInfoLabels();
        }

        private void OnScanStatusChanged(object? sender, string message)
        {
            _eventScanStatus = message;
            RefreshInfoLabels();
        }

        private void OnCloseQrDialogClicked(object? sender, EventArgs e)
        {
            QrDialogOverlay.IsVisible = false;
        }

        private void RefreshInfoLabels()
        {
            AutoStartValueLabel.Text = $"AutoStart: {_autoStart}";
            QRValueLabel.Text = $"QRValue: {(_qrValue.Length == 0 ? "(empty)" : _qrValue)}";
            ZoomValueLabel.Text = $"ZoomValue: {_zoomValue:0.0}";

            OnQRDetectedValueLabel.Text = $"OnQRDetected: {(_eventQrDetected.Length == 0 ? "(none)" : _eventQrDetected)}";
            OnZoomChangedValueLabel.Text = $"OnZoomChanged: {_eventZoomChanged:0.0}";
            OnScanStatusValueLabel.Text = $"OnScanStatus: {(_eventScanStatus.Length == 0 ? "(none)" : _eventScanStatus)}";
        }
    }
}
