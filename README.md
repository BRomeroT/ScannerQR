# ScannerQR

`ScannerQR` is a Blazor WebAssembly QR scanner component that uses browser camera APIs and emits scan events to .NET.

## Overview

- Target: **Blazor WebAssembly** (.NET 10)
- Detection engines:
  - `BarcodeDetector` (native, when available)
  - `jsQR` fallback
- Camera behavior:
  - Auto start/stop based on page lifecycle (focus/blur/visibility)
  - Pinch-to-zoom support on touch devices
- Architecture rule:
  - JavaScript scanner code only emits events
  - UI actions must be handled in Blazor (`Home.razor` / `Home.razor.cs`)

---

## Component API

> Component files:
- `Codeland.ScannerQR/Components/QRScanner.razor`
- `Codeland.ScannerQR/Components/QRScanner.razor.cs`

### Properties (Parameters)

Typical component parameters you should expose/use from Blazor:

- `VideoElementId` (`string`)
  - HTML id of the `<video>` used by scanner JS.
- `IsEnabled` (`bool`)
  - Enables/disables scanning flow.
- `InitialZoom` (`double?`)
  - Optional initial zoom value (if device supports zoom).

> If your local component has additional parameters, keep those as the source of truth.

### Events (Blazor callbacks)

Scanner JS emits these callbacks to .NET and the component/Home page should map them to UI/state logic:

- `OnQrDetected(string value)`
  - Fired when a non-duplicate QR value is detected.
- `OnScanStatus(string message)`
  - Fired on scanner status/errors (camera unavailable, missing detector, etc.).
- `OnZoomCapabilities(double minZoom, double maxZoom, double currentZoom)`
  - Fired after camera starts and after zoom updates.

Suggested public event callbacks from component to parent page:

- `EventCallback<string> QrDetected`
- `EventCallback<string> ScanStatusChanged`
- `EventCallback<(double Min, double Max, double Current)> ZoomCapabilitiesChanged`

### Methods

#### JavaScript public methods (`window.qrScanner`)

- `startAuto(dotNetRef, videoId)`
  - Binds lifecycle listeners and starts camera/detection.
- `setZoom(zoom)`
  - Applies clamped zoom to camera track (when supported).
- `dispose()`
  - Stops stream, cancels loop, unbinds listeners.

#### Internal JS callbacks invoked into .NET

- `OnQrDetected`
- `OnScanStatus`
- `OnZoomCapabilities`

---

## Usage

### 1) Add scripts in `wwwroot/index.html`

```html
<script src="https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.min.js"></script>
<script src="js/qrScanner.js"></script>
```

### 2) Import component namespace (`_Imports.razor`)

```razor
@using Codeland.QRScanner.Components
```

### 3) Use component in a page (example)

```razor
<QRScanner />
```

### 4) Handle UI in Blazor page/code-behind

- Open dialogs, show toasts, and route navigation in `Home.razor` / `Home.razor.cs`.
- Do not trigger UI behavior directly from scanner JavaScript.

---

## Blazor Implementation Pattern

Recommended split:

- `QRScanner.razor(.cs)`
  - JS interop bridge
  - Scanner lifecycle start/dispose
  - Raise events/data to parent
- `Home.razor(.cs)`
  - Business/UI actions (dialogs, confirmations, status banners)
  - React to `QrDetected` and status updates

This keeps scanner infrastructure reusable and UI-framework logic testable.

---

## Reuse in a New Blazor Project

Copy the following folders/files:

### Required

- `Components/QRScanner.razor`
- `Components/QRScanner.razor.cs`
- `wwwroot/js/qrScanner.js`

### Required host script references

Add in `wwwroot/index.html`:

```html
<script src="https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.min.js"></script>
<script src="js/qrScanner.js"></script>
```

### Recommended (sample integration)

- `Pages/Home.razor`
- `Pages/Home.razor.cs`

Use these as reference for event handling and UI orchestration.

### NuGet dependencies

In your `.csproj`:

- `Microsoft.AspNetCore.Components.WebAssembly`
- `Microsoft.AspNetCore.Components.WebAssembly.DevServer` (dev only)

### Namespace updates after copy

- Update `_Imports.razor` namespace to your app namespace.
- Update component/page namespaces if folder or project name changed.

---

## Notes and Constraints

- Camera permission is required.
- Zoom depends on device/browser support.
- Duplicate scan throttling is built in (short window).
- If browser lacks `BarcodeDetector`, `jsQR` fallback is used.

---

## Roadmap

- [ ] Binding Properties
- [ ] Create a library component
- [ ] Publish independent for NuGet package