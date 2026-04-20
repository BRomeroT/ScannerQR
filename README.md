# Codeland.QRScanner

**A cross-platform QR scanner component** — starting with Blazor WebAssembly, designed to grow toward MAUI, React, plain HTML and beyond.

`Codeland.QRScanner` gives any host application the ability to scan QR codes using the device camera, control zoom programmatically or through pinch gestures, and react to scan events — all without tight coupling to any specific UI framework.

---

## Table of Contents

- [Overview](#overview)
- [Current Target: Blazor](#current-target-blazor)
- [Component API](#component-api)
  - [Properties](#properties)
  - [Events](#events)
  - [Methods](#methods)
- [Zoom](#zoom)
- [How to Use in a Blazor Page](#how-to-use-in-a-blazor-page)
  - [1. Add Scripts](#1-add-scripts-in-wwwrootindexhtml)
  - [2. Register Namespace](#2-register-namespace)
  - [3. Add Component to Page](#3-add-component-to-page)
  - [4. Handle Events in Code-Behind](#4-handle-events-in-code-behind)
  - [5. Call Methods via Reference](#5-call-methods-via-reference)
- [Full Example](#full-example)
- [Architecture Rules](#architecture-rules)
- [Browser Compatibility](#browser-compatibility)
- [Camera Tips for Long-Range Scanning](#camera-tips-for-long-range-scanning)
- [Roadmap](#roadmap)

---

## Overview

| Feature | Detail |
|---|---|
| Current platform | Blazor WebAssembly (.NET 10) |
| QR detection engine | Native `BarcodeDetector` API + `jsQR` fallback |
| Zoom control | Hardware camera zoom slider + pinch-to-zoom gesture |
| Camera lifecycle | Auto-start, auto-pause on blur/hidden, auto-resume on focus |
| Architecture rule | JS emits events only — all UI handled in Blazor |

---

## Current Target: Blazor

The component lives in the namespace `Codeland.QRScanner` and consists of:

| File | Role |
|---|---|
| `Components/QRScanner.razor` | Component markup — video element and zoom slider |
| `Components/QRScanner.razor.cs` | Component code-behind — parameters, events, methods, JS interop |
| `wwwroot/js/qrScanner.js` | JS module — camera stream, detection loop, zoom, lifecycle listeners |
| `wwwroot/css/app.css` | Scanner and host page styles |

---

## Component API

### Properties

| Parameter | Type | Default | Description |
|---|---|---|---|
| `AutoStart` | `bool` | `true` | Starts the camera automatically on first render. Set to `false` to control start manually via `Start()`. |
| `QRValue` | `string` | `""` | The most recently detected QR code value. Updated on each successful scan. |
| `ZoomValue` | `double` | `1.0` | Current camera zoom level. Updated by slider, pinch gesture, or `Zoom()` method. |
| `FullPage` | `bool` | `true` | `true` — fills the entire viewport (`position:fixed`, `100vw × 100vh`). `false` — sized via `Width`, `Height`, `Class`, `Style`. |
| `Width` | `string?` | `null` | CSS width of the container (e.g. `"640px"`, `"100%"`, `"50vw"`). Ignored when `FullPage` is `true`. |
| `Height` | `string?` | `null` | CSS height of the container (e.g. `"480px"`, `"50vh"`). Ignored when `FullPage` is `true`. |
| `Class` | `string?` | `null` | Extra CSS class names applied to the container. Combined with the built-in class. |
| `Style` | `string?` | `null` | Inline CSS style applied to the container. Merged with `Width` and `Height` when `FullPage` is `false`. |

---

### Events

| Event | Argument Type | Description |
|---|---|---|
| `OnQRDetected` | `string` | Fired each time a QR code is successfully recognised. Duplicate values are suppressed for ~1.2 s to avoid rapid re-fires from the same code. |
| `OnZoomChanged` | `double` | Fired when the zoom level changes — via slider, pinch gesture, `Zoom()` method, or on camera start (reports initial zoom). |
| `OnScanStatus` | `string` | Fired with lifecycle and error messages: camera denied, fallback mode active, video element missing, etc. |

---

### Methods

Call methods using a `@ref` component reference (`QRScanner? _scanner`).

| Method | Signature | Description |
|---|---|---|
| `Start` | `Task Start()` | Opens the device camera and starts the QR detection loop. No-op if already running. |
| `Stop` | `Task Stop()` | Stops the camera stream and releases all camera resources. |
| `Scan` | `Task Scan()` | Semantic alias for `Start()`. Use when the intent is to begin a scan session. |
| `Zoom` | `Task Zoom(double zoomValue)` | Sets the camera zoom level. The value is clamped to the range the device camera supports. No effect if the camera does not report zoom capability. |

---

## Zoom

The component supports two ways to control zoom:

### Built-in slider

When the camera reports zoom capability, a slider is automatically shown overlaying the video. The user can drag it to adjust zoom. The component fires `OnZoomChanged` on every change.

### Pinch-to-zoom gesture

On touch devices, a two-finger pinch gesture is detected on the video element. The zoom level is calculated proportionally to the change in finger distance and applied via the camera hardware API. `OnZoomChanged` fires after each update.

### Programmatic zoom

Call `Zoom(double zoomValue)` from your Blazor page to set the zoom level at any time:

```csharp
await _scanner!.Zoom(2.5);  // 2.5× zoom
await _scanner!.Zoom(1.0);  // reset to no zoom
```

All three methods share the same underlying camera constraint API and fire `OnZoomChanged` with the applied value.

---

## How to Use in a Blazor Page

### 1. Add Scripts in `wwwroot/index.html`

Add these before the Blazor framework script tag:

```html
<script src="https://cdn.jsdelivr.net/npm/jsqr@1.4.0/dist/jsQR.min.js"></script>
<script src="js/qrScanner.js"></script>
```

### 2. Register Namespace

In `_Imports.razor` (global) or at the top of the page:

```razor
@using Codeland.QRScanner
```

### 3. Add Component to Page

**Full-page (default):**

```razor
<QRScanner AutoStart="true"
           OnQRDetected="HandleQRDetected"
           OnZoomChanged="HandleZoomChanged"
           OnScanStatus="HandleScanStatus" />
```

**Fixed size (embedded):**

```razor
<QRScanner FullPage="false"
           Width="640px"
           Height="360px"
           OnQRDetected="HandleQRDetected" />
```

**Relative / responsive size:**

```razor
<QRScanner FullPage="false"
           Width="100%"
           Height="50vh"
           Class="rounded shadow"
           OnQRDetected="HandleQRDetected" />
```

### 4. Handle Events in Code-Behind

```csharp
private Task HandleQRDetected(string value)
{
    _qrValue = value;
    _showDialog = true;   // open an HTML5 dialog — never alert() from JS
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

### 5. Call Methods via Reference

```razor
<QRScanner @ref="_scanner"
           AutoStart="false"
           OnQRDetected="HandleQRDetected" />

<button @onclick="() => _scanner!.Start()">▷ Start</button>
<button @onclick="() => _scanner!.Stop()">□ Stop</button>
<button @onclick="() => _scanner!.Zoom(2.0)">＋ Zoom 2×</button>
<button @onclick="() => _scanner!.Zoom(1.0)">－ Reset Zoom</button>
```

```csharp
private QRScanner? _scanner;
```

---

## Full Example

**Home.razor**

```razor
@page "/"
@using Codeland.QRScanner

<QRScanner @ref="_scanner"
           AutoStart="true"
           QRValue="@_qrValue"
           ZoomValue="@_zoomValue"
           OnQRDetected="HandleQRDetected"
           OnZoomChanged="HandleZoomChanged"
           OnScanStatus="HandleScanStatus" />

<div class="controls-overlay">
    <button @onclick="() => _scanner!.Start()">▷</button>
    <button @onclick="() => _scanner!.Stop()">□</button>
    <button @onclick="() => _scanner!.Scan()">⌾</button>
    <button @onclick="ZoomOut">－</button>
    <button @onclick="ZoomIn">＋</button>
    <span>QR: @_qrValue</span>
    <span>Zoom: @_zoomValue.ToString("0.0")x</span>
    <span>Status: @_status</span>
</div>

<dialog open="@_showDialog">
    <p>@_qrValue</p>
    <button @onclick="() => _showDialog = false">OK</button>
</dialog>
```

**Home.razor.cs**

```csharp
using Codeland.QRScanner;

public partial class Home
{
    private QRScanner? _scanner;

    private string _qrValue = string.Empty;
    private double _zoomValue = 1;
    private string _status = string.Empty;
    private bool _showDialog;

    private async Task ZoomIn()
    {
        _zoomValue = Math.Min(_zoomValue + 0.2, 10);
        await _scanner!.Zoom(_zoomValue);
    }

    private async Task ZoomOut()
    {
        _zoomValue = Math.Max(_zoomValue - 0.2, 1);
        await _scanner!.Zoom(_zoomValue);
    }

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
        _status = message;
        return Task.CompletedTask;
    }
}
```

---

## Architecture Rules

| Rule | Reason |
|---|---|
| JavaScript only emits events (`invokeMethodAsync`) | Keeps JS side clean and platform-agnostic |
| All UI actions (dialogs, navigation, alerts) handled in Blazor | Separation of concerns — JS has no dependency on Blazor UI |
| Camera stops on `window.blur` / `document.hidden` | Saves battery, respects privacy |
| Camera resumes on `window.focus` / visibility restored | Seamless UX when switching apps |
| Duplicate QR suppression (~1.2 s) | Prevents rapid re-fires from a steady QR in frame |

---

## Browser Compatibility

| Browser | QR Detection | Hardware Zoom | Pinch-to-Zoom |
|---|---|---|---|
| Chrome (desktop) | ✅ Native `BarcodeDetector` | ⚠️ Hardware dependent | ❌ No touch |
| Edge (desktop) | ✅ Native `BarcodeDetector` | ⚠️ Hardware dependent | ❌ No touch |
| Chrome (Android) | ✅ Native `BarcodeDetector` | ✅ If camera supports it | ✅ |
| Edge (Android) | ✅ Native `BarcodeDetector` | ✅ If camera supports it | ✅ |
| Safari (iOS) | ✅ jsQR fallback | ⚠️ Hardware dependent | ✅ |
| Firefox | ✅ jsQR fallback | ⚠️ Hardware dependent | ✅ |

---

## Camera Tips for Long-Range Scanning

| Tip | Detail |
|---|---|
| Use optical zoom hardware | A telephoto lens or PTZ camera gives the best results at 15+ m |
| Increase QR code size | At 15 m, aim for a minimum 30 × 30 cm printed code |
| Ensure good lighting | Avoid backlit or low-contrast QR codes |
| Use high resolution | The component requests 1920 × 1080 by default |
| Use zoom controls | Increase zoom via slider, pinch, or `Zoom()` before the decoder reads the frame |

---

## Roadmap

### ✅ Done (Blazor WebAssembly)

- [x] Real-time QR scanning via device camera
- [x] Native `BarcodeDetector` with `jsQR` fallback
- [x] Auto-start / auto-pause on window lifecycle events
- [x] Built-in zoom slider (shown when camera supports zoom)
- [x] Pinch-to-zoom gesture on touch devices
- [x] Programmatic zoom via `Zoom(double)` method
- [x] Full-page and custom-size modes (`FullPage`, `Width`, `Height`, `Class`, `Style`)
- [x] Component properties: `AutoStart`, `QRValue`, `ZoomValue`
- [x] Component events: `OnQRDetected`, `OnZoomChanged`, `OnScanStatus`
- [x] Component methods: `Start`, `Stop`, `Scan`, `Zoom`
- [x] Duplicate QR suppression
- [x] JS event-only architecture (no UI in JS)
- [x] XML documentation on all public API members
- [x] JSDoc documentation on all JS functions

### 🔜 Pending — Blazor

- [ ] Publish as NuGet package (`Codeland.QRScanner`)
- [ ] Support for multiple simultaneous QR codes in a single frame
- [ ] Torch / flashlight toggle
- [ ] QR format filter (QR only, Aztec, Code128, etc.)
- [ ] Scan region overlay (visible frame guide)
- [ ] Vibration feedback on detection (mobile)
- [ ] Sound feedback on detection
- [ ] Configurable duplicate suppression timeout

### 🗺️ Future Platforms

| Platform | Status | Notes |
|---|---|---|
| **.NET MAUI** | Planned | Native camera via `MediaCapture` / `AVFoundation`, same event API |
| **React** | Planned | JS-only component wrapping the same `qrScanner.js` module |
| **Plain HTML / Vanilla JS** | Planned | Standalone web component (`<qr-scanner>` custom element) |
| **Angular** | Considering | Angular wrapper around the JS module |
| **Vue** | Considering | Vue 3 composable + component wrapper |