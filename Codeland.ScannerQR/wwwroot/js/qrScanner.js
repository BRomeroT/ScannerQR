/**
 * <summary>
 * Browser-side QR scanner module.
 *
 * Responsibilities:
 * - Manage camera lifecycle (start/stop/restart)
 * - Detect QR values using BarcodeDetector or jsQR fallback
 * - Emit scan/zoom/status events to .NET via DotNetObjectReference
 * - Handle pinch-to-zoom on touch devices
 *
 * Note: UI actions must be handled in Blazor (Home page/code-behind).
 * </summary>
 */
window.qrScanner = (() => {
    let stream = null;
    let track = null;
    let detector = null;
    let useJsQr = false;
    let canvas = null;
    let ctx = null;

    let video = null;
    let dotNetRef = null;
    let frameRequest = 0;
    let lastValue = "";
    let lastDetectedAt = 0;

    let minZoom = 1;
    let maxZoom = 1;
    let currentZoom = 1;

    let desiredRunning = false;
    let isStarting = false;
    let lifecycleBound = false;
    let pinchBound = false;

    let pinchStartDistance = null;
    let pinchStartZoom = null;

    /**
     * <summary>Gets Euclidean distance between two touch points.</summary>
     * <param name="touchA">First touch.</param>
     * <param name="touchB">Second touch.</param>
     * <returns>Distance in pixels.</returns>
     */
    function getTouchDistance(touchA, touchB) {
        const dx = touchA.clientX - touchB.clientX;
        const dy = touchA.clientY - touchB.clientY;
        return Math.sqrt((dx * dx) + (dy * dy));
    }

    /**
     * <summary>Sends a status message to .NET.</summary>
     * <param name="message">Status text.</param>
     * <returns>Promise resolved when callback completes.</returns>
     */
    async function setStatus(message) {
        if (!dotNetRef) {
            return;
        }

        try {
            await dotNetRef.invokeMethodAsync("OnScanStatus", message);
        } catch {
        }
    }

    /**
     * <summary>
     * Initializes the QR detection engine.
     * Uses BarcodeDetector when available, otherwise jsQR fallback.
     * </summary>
     * <returns>True when a detector is available; otherwise false.</returns>
     */
    async function createDetector() {
        useJsQr = false;
        detector = null;

        if ("BarcodeDetector" in window) {
            detector = new BarcodeDetector({ formats: ["qr_code"] });
            return true;
        }

        if (typeof window.jsQR === "function") {
            useJsQr = true;
            return true;
        }

        await setStatus("QR scanning unavailable: browser doesn't support BarcodeDetector and jsQR fallback is missing.");
        return false;
    }

    /**
     * <summary>Binds pinch gesture handlers to video element.</summary>
     */
    function bindPinch() {
        if (!video || pinchBound) {
            return;
        }

        video.addEventListener("touchstart", onTouchStart, { passive: true });
        video.addEventListener("touchmove", onTouchMove, { passive: false });
        video.addEventListener("touchend", onTouchEnd, { passive: true });
        video.addEventListener("touchcancel", onTouchEnd, { passive: true });
        pinchBound = true;
    }

    /**
     * <summary>Unbinds pinch gesture handlers from video element.</summary>
     */
    function unbindPinch() {
        if (!video || !pinchBound) {
            return;
        }

        video.removeEventListener("touchstart", onTouchStart);
        video.removeEventListener("touchmove", onTouchMove);
        video.removeEventListener("touchend", onTouchEnd);
        video.removeEventListener("touchcancel", onTouchEnd);
        pinchBound = false;
        pinchStartDistance = null;
        pinchStartZoom = null;
    }

    /**
     * <summary>Binds window/document lifecycle handlers.</summary>
     */
    function bindLifecycle() {
        if (lifecycleBound) {
            return;
        }

        window.addEventListener("focus", onWindowFocus);
        window.addEventListener("blur", onWindowBlur);
        window.addEventListener("beforeunload", onPageLeave);
        window.addEventListener("pagehide", onPageLeave);
        document.addEventListener("visibilitychange", onVisibilityChanged);

        lifecycleBound = true;
    }

    /**
     * <summary>Unbinds window/document lifecycle handlers.</summary>
     */
    function unbindLifecycle() {
        if (!lifecycleBound) {
            return;
        }

        window.removeEventListener("focus", onWindowFocus);
        window.removeEventListener("blur", onWindowBlur);
        window.removeEventListener("beforeunload", onPageLeave);
        window.removeEventListener("pagehide", onPageLeave);
        document.removeEventListener("visibilitychange", onVisibilityChanged);

        lifecycleBound = false;
    }

    /**
     * <summary>
     * Starts camera and detection pipeline when requested and allowed by page state.
     * Safe to call repeatedly.
     * </summary>
     * <returns>Promise that completes when start flow finishes.</returns>
     */
    async function ensureStarted() {
        if (!desiredRunning || isStarting || stream) {
            return;
        }

        if (document.hidden || !document.hasFocus()) {
            return;
        }

        if (!video || !dotNetRef) {
            return;
        }

        isStarting = true;

        try {
            const detectorReady = await createDetector();
            if (!detectorReady) {
                return;
            }

            try {
                stream = await navigator.mediaDevices.getUserMedia({
                    video: {
                        facingMode: { ideal: "environment" },
                        width: { ideal: 1920 },
                        height: { ideal: 1080 },
                        focusMode: "continuous"
                    },
                    audio: false
                });
            } catch {
                await setStatus("Camera access denied or unavailable.");
                return;
            }

            video.srcObject = stream;

            try {
                await video.play();
            } catch {
                await setStatus("Unable to start video preview.");
                stopStream();
                return;
            }

            track = stream.getVideoTracks()[0];

            minZoom = 1;
            maxZoom = 1;
            currentZoom = 1;

            const caps = track.getCapabilities ? track.getCapabilities() : null;
            const settings = track.getSettings ? track.getSettings() : null;

            if (caps && caps.zoom) {
                minZoom = caps.zoom.min ?? 1;
                maxZoom = caps.zoom.max ?? 1;
                currentZoom = settings?.zoom ?? minZoom;
            }

            await dotNetRef.invokeMethodAsync("OnZoomCapabilities", minZoom, maxZoom, currentZoom);

            if (caps && caps.focusMode && caps.focusMode.includes("continuous")) {
                try {
                    await track.applyConstraints({ advanced: [{ focusMode: "continuous" }] });
                } catch {
                }
            }

            if (useJsQr) {
                canvas = document.createElement("canvas");
                ctx = canvas.getContext("2d", { willReadFrequently: true });
            }

            detectLoop();
        } finally {
            isStarting = false;
        }
    }

    /**
     * <summary>Runs one frame detection with jsQR fallback path.</summary>
     * <returns>Detected QR value or empty string.</returns>
     */
    function detectUsingJsQr() {
        if (!video || !ctx || !canvas || video.videoWidth <= 0 || video.videoHeight <= 0) {
            return "";
        }

        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
        const result = window.jsQR(imageData.data, imageData.width, imageData.height, {
            inversionAttempts: "dontInvert"
        });

        return result?.data ?? "";
    }

    /**
     * <summary>
     * Main detection loop driven by requestAnimationFrame.
     * Emits OnQrDetected for non-duplicate values.
     * </summary>
     * <returns>Promise used by async detection calls.</returns>
     */
    async function detectLoop() {
        if (!stream || !video || (!detector && !useJsQr)) {
            return;
        }

        try {
            if (video.readyState >= 2) {
                let value = "";

                if (detector) {
                    const results = await detector.detect(video);
                    if (results && results.length > 0) {
                        value = results[0].rawValue ?? "";
                    }
                } else if (useJsQr) {
                    value = detectUsingJsQr();
                }

                if (value) {
                    const now = Date.now();
                    const isDuplicate = value === lastValue && (now - lastDetectedAt) < 1200;

                    if (!isDuplicate) {
                        lastValue = value;
                        lastDetectedAt = now;
                        await dotNetRef.invokeMethodAsync("OnQrDetected", value);
                    }
                }
            }
        } catch {
        }

        frameRequest = requestAnimationFrame(detectLoop);
    }

    /**
     * <summary>Stops frame loop and camera stream, and clears detector state.</summary>
     */
    function stopStream() {
        if (frameRequest) {
            cancelAnimationFrame(frameRequest);
            frameRequest = 0;
        }

        if (stream) {
            stream.getTracks().forEach(t => t.stop());
        }

        if (video) {
            video.srcObject = null;
        }

        stream = null;
        track = null;
        detector = null;
        useJsQr = false;
        canvas = null;
        ctx = null;
    }

    /**
     * <summary>Applies zoom value to current camera track.</summary>
     * <param name="zoom">Desired zoom level.</param>
     * <returns>Promise resolved when constraints are applied.</returns>
     */
    async function setZoom(zoom) {
        const value = Number(zoom);

        if (!track || !track.applyConstraints || Number.isNaN(value)) {
            return;
        }

        const clamped = Math.min(maxZoom, Math.max(minZoom, value));

        try {
            await track.applyConstraints({ advanced: [{ zoom: clamped }] });
            currentZoom = clamped;
            await dotNetRef.invokeMethodAsync("OnZoomCapabilities", minZoom, maxZoom, currentZoom);
        } catch {
        }
    }

    /**
     * <summary>Touch start handler for pinch-to-zoom initialization.</summary>
     * <param name="e">Touch event.</param>
     */
    function onTouchStart(e) {
        if (!track || maxZoom <= minZoom || e.touches.length !== 2) {
            return;
        }

        pinchStartDistance = getTouchDistance(e.touches[0], e.touches[1]);
        pinchStartZoom = currentZoom;
    }

    /**
     * <summary>Touch move handler for pinch-to-zoom updates.</summary>
     * <param name="e">Touch event.</param>
     */
    function onTouchMove(e) {
        if (!track || maxZoom <= minZoom || e.touches.length !== 2 || !pinchStartDistance || !pinchStartZoom) {
            return;
        }

        e.preventDefault();

        const currentDistance = getTouchDistance(e.touches[0], e.touches[1]);
        const ratio = currentDistance / pinchStartDistance;
        const nextZoom = pinchStartZoom * ratio;
        void setZoom(nextZoom);
    }

    /**
     * <summary>Touch end/cancel handler that resets pinch state.</summary>
     */
    function onTouchEnd() {
        pinchStartDistance = null;
        pinchStartZoom = null;
    }

    /**
     * <summary>Window blur handler that releases camera resources.</summary>
     */
    function onWindowBlur() {
        stopStream();
    }

    /**
     * <summary>Window focus handler that attempts to restart scanning.</summary>
     */
    function onWindowFocus() {
        void ensureStarted();
    }

    /**
     * <summary>Visibility change handler that stops/starts scanner by tab visibility.</summary>
     */
    function onVisibilityChanged() {
        if (document.hidden) {
            stopStream();
            return;
        }

        void ensureStarted();
    }

    /**
     * <summary>Page leave handler that guarantees stream cleanup.</summary>
     */
    function onPageLeave() {
        stopStream();
    }

    /**
     * <summary>
     * Public API: starts scanner with lifecycle automation.
     * </summary>
     * <param name="ref">DotNetObjectReference exposing callback methods.</param>
     * <param name="videoId">Target video element id.</param>
     * <returns>Promise resolved when startup attempt completes.</returns>
     */
    async function startAuto(ref, videoId) {
        dotNetRef = ref;
        video = document.getElementById(videoId);

        if (!video) {
            await setStatus("Video element not found.");
            return;
        }

        desiredRunning = true;
        bindLifecycle();
        bindPinch();

        await ensureStarted();
    }

    /**
     * <summary>
     * Public API: disposes scanner resources and event handlers.
     * </summary>
     */
    function dispose() {
        desiredRunning = false;
        stopStream();
        unbindPinch();
        unbindLifecycle();

        video = null;
        dotNetRef = null;
        lastValue = "";
        lastDetectedAt = 0;
    }

    return {
        startAuto,
        setZoom,
        dispose
    };
})();
