# Codeland.QRScanner

**A cross-platform QR scanner component suite** for applications that need camera-based QR recognition, zoom control, and event-driven UI integration.

`Codeland.QRScanner` is designed around a simple idea: **camera and decoding logic stay inside the platform implementation, while UI reactions stay in the host application**. The project began with Blazor WebAssembly and now includes a .NET MAUI implementation for Android, iOS, MacCatalyst, and Windows.

---

## Table of Contents

- [Overview](#overview)
- [Goals](#goals)
- [Platform Status](#platform-status)
- [Shared Scanner Concepts](#shared-scanner-concepts)
  - [Core Capabilities](#core-capabilities)
  - [Shared Behaviour](#shared-behaviour)
- [Platform Implementations](#platform-implementations)
  - [Blazor WebAssembly](#blazor-webassembly)
    - [Files](#files)
    - [API](#api)
    - [How to Use in a Blazor Page](#how-to-use-in-a-blazor-page)
    - [Full Blazor Example](#full-blazor-example)
    - [Browser Compatibility](#browser-compatibility)
  - [.NET MAUI](#net-maui)
    - [Files](#files-1)
    - [API](#api-1)
    - [How to Use in a MAUI Page](#how-to-use-in-a-maui-page)
    - [Platform Notes](#platform-notes)
- [Architecture Rules](#architecture-rules)
- [Camera Tips for Long-Range Scanning](#camera-tips-for-long-range-scanning)
- [Roadmap](#roadmap)

---

## Overview

| Feature | Detail |
|---|---|
| Primary purpose | Scan QR codes from the device camera |
| Interaction model | Event-driven host UI |
| Zoom control | Buttons, programmatic zoom, and pinch gesture where supported |
| Supported app models | Blazor WebAssembly, .NET MAUI |
| Current MAUI targets | Android, iOS, MacCatalyst, Windows |
| Design rule | Scanner implementation emits state/events; host app owns UI |

---

## Goals

| Goal | Detail |
|---|---|
| Reusable | Keep scanning logic encapsulated behind a platform-specific component/view |
| Cross-platform | Provide the same conceptual scanner behaviour across web and native apps |
| Event-driven | Let host pages decide what to show when QR codes are detected |
| Zoom-capable | Support camera zoom through UI buttons, gestures, and code |
| Low coupling | Avoid hard-coding platform UI behaviour into scanner internals |
| Extensible | Leave room for torch, scan region, multiple codes, and packaging later |

---

## Platform Status

| Platform | Status | Notes |
|---|---|---|
| **Blazor WebAssembly** | ✅ Implemented | Uses browser camera APIs plus `jsQR` fallback |
| **.NET MAUI Android** | ✅ Implemented | Uses CameraX + ZXing.Net |
| **.NET MAUI Windows** | ✅ Implemented | Uses `MediaCapture` + ZXing.Net |
| **.NET MAUI iOS** | ✅ Implemented | Uses AVFoundation native QR metadata detection |
| **.NET MAUI MacCatalyst** | ✅ Implemented | Uses AVFoundation native QR metadata detection |
| **React** | 🔜 Planned | Wrapper around shared web scanner approach |
| **Plain HTML / Vanilla JS** | 🔜 Planned | Standalone web component or simple JS integration |

> Note: Android and Windows implementations have been runtime-validated in this workspace. Apple platform code is implemented and build-safe, but still needs on-device/runtime validation when a Mac is available.

---

## Shared Scanner Concepts

### Core Capabilities

| Capability | Detail |
|---|---|
| QR detection | Detects QR content from the active camera feed |
| Duplicate suppression | Repeated scans of the same QR are suppressed for ~1.2 s |
| Zoom feedback | Host app is notified whenever zoom changes |
| Start / stop lifecycle | Camera session can be started and stopped programmatically |
| Host-owned UI | Dialogs, labels, alerts, and overlays belong to the consuming app |

---

### Shared Behaviour

| Behaviour | Detail |
|---|---|
| Auto-start | Scanner can begin automatically on first render/load |
| Zoom clamping | Requested zoom is clamped to device-supported values |
| Event reporting | Status messages are raised for permission and lifecycle conditions |
| Gesture zoom | Pinch-to-zoom is used on platforms that expose touch camera zoom easily |
| Programmatic control | Host code can start, stop, and zoom the scanner explicitly |

---

## Platform Implementations

## Blazor WebAssembly

Blazor provides the original web implementation. It is centered on a reusable Razor component and a JavaScript module that manages browser camera APIs.

### Files

| File | Role |
|---|---|
| `Blazor/Codeland.ScannerQR/Components/QRScanner.razor` | Component markup — video element and zoom slider |
| `Blazor/Codeland.ScannerQR/Components/QRScanner.razor.cs` | Component logic — parameters, events, methods, JS interop |
| `Blazor/Codeland.ScannerQR/Pages/Home.razor` | Host page showing scanner UI, buttons, labels, and dialog |
| `Blazor/Codeland.ScannerQR/Pages/Home.razor.cs` | Host page event handling and UI state |
| `Blazor/Codeland.ScannerQR/wwwroot/js/qrScanner.js` | Browser camera lifecycle, detection loop, zoom, and event forwarding |
| `Blazor/Codeland.ScannerQR/wwwroot/css/app.css` | Scanner and host page styles |

---

### API

#### Properties

| Parameter | Type | Default | Description |
|---|---|---|---|
| `AutoStart` | `bool` | `true` | Starts the camera automatically on first render. Set to `false` to control start manually. |
| `QRValue` | `string` | `""` | The most recently detected QR code value. |
| `ZoomValue` | `double` | `1.0` | Current camera zoom level. |
| `FullPage` | `bool` | `true` | Makes the component fill the viewport. |
| `Width` | `string?` | `null` | Custom width when `FullPage` is `false`. |
| `Height` | `string?` | `null` | Custom height when `FullPage` is `false`. |
| `Class` | `string?` | `null` | Extra CSS class names for the container. |
| `Style` | `string?` | `null` | Inline CSS applied to the container. |

#### Events

| Event | Argument Type | Description |
|---|---|---|
| `OnQRDetected` | `string` | Fired when a QR code is recognised. |
| `OnZoomChanged` | `double` | Fired when zoom changes. |
| `OnScanStatus` | `string` | Fired for lifecycle, permission, and error/status messages. |

#### Methods

Call methods using a `@ref` component reference (`QRScanner? _scanner`).

| Method | Signature | Description |
|---|---|---|
| `Start` | `Task Start()` | Starts the web camera and detection loop. |
| `Stop` | `Task Stop()` | Stops the camera and releases resources. |
| `Scan` | `Task Scan()` | Semantic alias for `Start()`. |
| `Zoom` | `Task Zoom(double zoomValue)` | Applies a zoom value supported by the device camera. |

---

### How to Use in a Blazor Page

#### 1. Add Scripts in `wwwroot/index.html`

Add these before the Blazor framework script tag:

```html
<script src="https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.min.js"></script>
<script src="js/qrScanner.js"></script>
```

#### 2. Register Namespace

In `_Imports.razor` or at the top of the page:

```razor
@using Codeland.QRScanner
```

#### 3. Add Component to Page

```razor
<QRScanner AutoStart="true"
           OnQRDetected="HandleQRDetected"
           OnZoomChanged="HandleZoomChanged"
           OnScanStatus="HandleScanStatus" />
```

#### 4. Handle Events in Code-Behind

```csharp
private Task HandleQRDetected(string value)
{
    _qrValue = value;
    _showDialog = true;
    return Task.CompletedTask;
}

private Task HandleZoomChanged(double zoom)
{
    _zoomValue = zoom;
    return Task.CompletedTask;
}

private Task HandleScanStatus(string message)
{
    _statusMessage = message;
    return Task.CompletedTask;
}
```

#### 5. Call Methods via Reference

```razor
<QRScanner @ref="_scanner"
           AutoStart="false"
           OnQRDetected="HandleQRDetected" />

<button @onclick="() => _scanner!.Start()">▷ Start</button>
<button @onclick="() => _scanner!.Stop()">□ Stop</button>
<button @onclick="() => _scanner!.Zoom(2.0)">＋ Zoom 2×</button>
```

---

### Full Blazor Example

The current host page demonstrates a full scanner UI with:

| Area | Detail |
|---|---|
| Control buttons | Start, Stop, Scan, Zoom Out, Zoom In |
| Properties panel | `AutoStart`, `QRValue`, `ZoomValue` |
| Events panel | `OnQRDetected`, `OnZoomChanged`, `OnScanStatus` |
| Dialog | Displays the last detected QR value |

See:
- `Blazor/Codeland.ScannerQR/Pages/Home.razor`
- `Blazor/Codeland.ScannerQR/Pages/Home.razor.cs`

---

### Browser Compatibility

| Browser | QR Detection | Hardware Zoom | Pinch-to-Zoom |
|---|---|---|---|
| Chrome (desktop) | ✅ Native `BarcodeDetector` | ⚠️ Hardware dependent | ❌ No touch |
| Edge (desktop) | ✅ Native `BarcodeDetector` | ⚠️ Hardware dependent | ❌ No touch |
| Chrome (Android) | ✅ Native `BarcodeDetector` | ✅ If camera supports it | ✅ |
| Edge (Android) | ✅ Native `BarcodeDetector` | ✅ If camera supports it | ✅ |
| Safari (iOS) | ✅ `jsQR` fallback | ⚠️ Hardware dependent | ✅ |
| Firefox | ✅ `jsQR` fallback | ⚠️ Hardware dependent | ✅ |

---

## .NET MAUI

The MAUI implementation provides a native scanner view with platform handlers for Android, Windows, iOS, and MacCatalyst.

> There is currently no separate “copy files” setup step for MAUI in this repository. The MAUI sample project already contains the scanner view, shared handler mapper, platform handlers, and host page example in-place.

### Files

| File | Role |
|---|---|
| `MAUI/Codeland.ScannerQR/Controls/QRScannerView.cs` | Cross-platform MAUI scanner view API |
| `MAUI/Codeland.ScannerQR/Handlers/QRScannerHandler.cs` | Shared mapper for scanner commands |
| `MAUI/Codeland.ScannerQR/Platforms/Android/Handlers/QRScannerHandler.cs` | Android CameraX + ZXing.Net implementation |
| `MAUI/Codeland.ScannerQR/Platforms/Windows/Handlers/QRScannerHandler.cs` | Windows `MediaCapture` + ZXing.Net implementation |
| `MAUI/Codeland.ScannerQR/Platforms/iOS/Handlers/QRScannerHandler.cs` | iOS AVFoundation implementation |
| `MAUI/Codeland.ScannerQR/Platforms/MacCatalyst/Handlers/QRScannerHandler.cs` | MacCatalyst AVFoundation implementation |
| `MAUI/Codeland.ScannerQR/MainPage.xaml` | Example host UI with scanner, control buttons, labels, and dialog overlay |
| `MAUI/Codeland.ScannerQR/MainPage.xaml.cs` | Example host page logic for properties, events, and QR dialog handling |

---

### API

#### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `AutoStart` | `bool` | `true` | Starts the camera automatically when the view is first rendered. |
| `ZoomValue` | `double` | `1.0` | Current zoom level reported by the active platform handler. |
| `IsRunning` | `bool` | `false` | Indicates whether the scanner is currently active. |

#### Events

| Event | Argument Type | Description |
|---|---|---|
| `QRDetected` | `string` | Fired when a QR value is decoded. |
| `ZoomChanged` | `double` | Fired when zoom changes due to code, buttons, or pinch. |
| `ScanStatusChanged` | `string` | Fired for lifecycle, permission, and error/status messages. |

#### Methods

| Method | Signature | Description |
|---|---|---|
| `StartScanning` | `void StartScanning()` | Starts the native camera scanner. |
| `StopScanning` | `void StopScanning()` | Stops the scanner and releases resources. |
| `Zoom` | `void Zoom(double zoomValue)` | Sets the scanner zoom level. |
| `ApplyZoom` | `void ApplyZoom(double zoom)` | Internal-facing zoom command path used by handlers/bindable state. |

---

### How to Use in a MAUI Page

#### 1. Add the scanner control to XAML

```xml
<controls:QRScannerView x:Name="Scanner"
                        AutoStart="True"
                        QRDetected="OnQRDetected"
                        ZoomChanged="OnZoomChanged"
                        ScanStatusChanged="OnScanStatusChanged" />
```

#### 2. Handle events in code-behind

```csharp
private void OnQRDetected(object? sender, string value)
{
    _qrValue = value;
    QrDialogOverlay.IsVisible = true;
}

private void OnZoomChanged(object? sender, double zoom)
{
    _zoomValue = zoom;
}

private void OnScanStatusChanged(object? sender, string message)
{
    _status = message;
}
```

#### 3. Control the scanner programmatically

```csharp
Scanner.StartScanning();
Scanner.Zoom(2.0);
Scanner.StopScanning();
```

#### 4. Build host-owned UI

The sample MAUI page mirrors the Blazor demo by showing:

| Area | Detail |
|---|---|
| Control buttons | Start, Stop, Scan, Zoom Out, Zoom In |
| Properties panel | `AutoStart`, `QRValue`, `ZoomValue` |
| Events panel | `OnQRDetected`, `OnZoomChanged`, `OnScanStatus` |
| Dialog overlay | Displays the detected QR value with an OK button |

---

### Platform Notes

| MAUI Platform | Camera Stack | QR Detection | Zoom | Notes |
|---|---|---|---|---|
| Android | CameraX | ZXing.Net over image analysis frames | Buttons + pinch + programmatic | Runtime-validated in this workspace |
| Windows | `MediaCapture` + frame reader | ZXing.Net over software bitmaps | Buttons + programmatic | Runtime-validated in this workspace |
| iOS | AVFoundation | Native `AVCaptureMetadataOutput` | Buttons + pinch + programmatic | Build-validated, runtime validation pending |
| MacCatalyst | AVFoundation | Native `AVCaptureMetadataOutput` | Buttons + pinch + programmatic | Build-validated, runtime validation pending |

---

## Architecture Rules

| Rule | Reason |
|---|---|
| Host app owns UI | Dialogs, labels, alerts, navigation, and layout belong to the consumer page/view |
| Platform implementation owns camera work | Each platform handler/component is responsible for preview, capture, decode, and zoom plumbing |
| Events report scanner state | Host UI updates from scan, zoom, and status events |
| Duplicate QR suppression (~1.2 s) | Prevents rapid re-fires from the same code staying in frame |
| Zoom must be device-safe | Requested zoom is clamped to the device-supported range |

---

## Camera Tips for Long-Range Scanning

| Tip | Detail |
|---|---|
| Use optical zoom hardware | A telephoto lens or PTZ camera gives the best results at 15+ m |
| Increase QR code size | At 15 m, aim for a minimum 30 × 30 cm printed code |
| Ensure good lighting | Avoid backlit or low-contrast QR codes |
| Use high resolution | The implementations request high-resolution camera feeds where possible |
| Use zoom controls | Increase zoom via buttons, pinch, or `Zoom()` before the decoder reads the frame |

---

## Roadmap

### ✅ Done

- [x] Blazor WebAssembly QR scanner
- [x] Native/browser QR detection with fallback on web
- [x] .NET MAUI QR scanner view and handlers
- [x] Android QR detection and pinch zoom
- [x] Windows QR detection and preview rendering
- [x] iOS and MacCatalyst native AVFoundation handlers
- [x] Programmatic zoom support
- [x] Duplicate QR suppression
- [x] Host-side sample UI for Blazor and MAUI
- [x] XML documentation on scanner view and handlers

### 🔜 Next

- [ ] Publish as NuGet package (`Codeland.QRScanner`)
- [ ] Convert to MAUI Controls
- [ ] Add bindable properties patterns to all platforms
- [ ] Torch / flashlight toggle
- [ ] Multiple simultaneous QR detection
- [ ] QR format filtering
- [ ] Scan region overlay driven by actual decode crop area
- [ ] Vibration / sound feedback on supported devices
- [ ] Configurable duplicate suppression timeout
- [ ] Runtime validation on iOS and MacCatalyst devices

### 🗺️ Future Platforms

| Platform | Status | Notes |
|---|---|---|
| **React** | Planned | Wrapper around the web scanner implementation |
| **Plain HTML / Vanilla JS** | Planned | Standalone web component or minimal JS integration |
| **Angular** | Considering | Angular wrapper around the web scanner |
| **Vue** | Considering | Vue wrapper around the web scanner |