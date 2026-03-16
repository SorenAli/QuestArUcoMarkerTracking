# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projektübersicht

Unity 6 (6000.3.2f1) AR-Anwendung für Meta Quest. ArUco-Marker werden per OpenCV erkannt, 3D-GameObjects per `solvePnP` im Raum platziert. Bei erster Erkennung wird ein `OVRSpatialAnchor` gesetzt, der das Objekt dauerhaft im Raum verankert.

**Namespace:** `TryAR.MarkerTracking`
**Aktive Szene:** `Assets/1 - ArUcoMarkerTracking.unity`
**Build-Ziel:** Android (Meta Quest), IL2CPP, arm64

---

## Build

Android Build aus Unity Editor: **File → Build Settings → Build And Run**

**Bekanntes Problem – Meta XR SDK 85.0.0 Bug:**
`UpdateManifestWithCodeSample` sucht rekursiv nach allen `AndroidManifest.xml` und findet dabei veraltete Gradle-Build-Artefakte aus vorherigen Builds. Der Workaround `Assets/Editor/MetaXRManifestWorkaround.cs` löscht das `xrmanifest.androidlib/build/` Verzeichnis vor dem Meta-SDK-Callback (callbackOrder -1).

**Bei Out-of-Memory Fehler (llvm-objcopy):**
Build Settings → **"Create symbols.zip" → Disabled** oder **Development Build** aktivieren.

---

## Architektur

```
PassthroughCameraAccess (Meta XR SDK)
        │
        ▼
ArUcoTrackingAppCoordinator (MonoBehaviour)
  ├── Start()              → wartet auf Kamera, liest Intrinsics, initialisiert Tracker
  ├── Update()
  │     ├── UpdateCameraPoses()       → setzt m_cameraAnchor auf aktuelle Kamerapose
  │     ├── ProcessMarkerTracking()
  │     │     ├── DetectMarker()      → OpenCV: Texture → Mat → detectMarkers()
  │     │     ├── EstimatePoseCanonicalMarker()  → solvePnP → Transform (nur ohne Anchor)
  │     │     └── CreateAnchorAsync() → OVRSpatialAnchor auf GameObject, session-only
  │     ├── CheckAnchorRefresh()      → per-Marker-Timer + Button B
  │     └── HandleVisualizationToggle() → OVR Button.One togglet Debug-View
  └── m_markerAnchors: Dictionary<int, OVRSpatialAnchor>  (pro Marker-ID)
```

**Spatial Anchor Flow:**
1. Marker erkannt → `EstimatePose` positioniert das GameObject, Stabilisierungszähler (`_markerDetectionCounts`) zählt hoch
2. Nach `_stabilizationFrameCount` Frames (default 10): Pose-Smoothing hat konvergiert → `CreateAnchorAsync` startet
3. Marker in `m_pendingAnchorMarkerIds` → `EstimatePose` ignoriert ihn → Objekt friert ein
4. Nach `WhenLocalizedAsync()`: `OVRSpatialAnchor` übernimmt Transform-Kontrolle dauerhaft; Zeitstempel in `_markerAnchorTimes` gespeichert
5. Bei Fehler: Anchor wird entfernt, nächste Detection versucht es erneut

**Anchor Refresh:**
- Jeder Marker hat seinen eigenen unabhängigen Timer (`_markerAnchorTimes: Dictionary<int, float>`).
- Nach `_anchorRefreshInterval` (default 10s): Marker kommt in `_markersNeedingRefresh`. Reset passiert erst wenn der Marker wieder sichtbar ist.
- **Button B (OVRInput.Button.Two, gehalten):** Solange gedrückt werden alle sichtbaren Marker-Anchors kontinuierlich zurückgesetzt → Objekt folgt dem Marker live. Beim Loslassen wird neuer Anchor fixiert.
- **Button A (OVRInput.Button.One):** Togglet Debug-Kameraansicht.

Zwei parallele Varianten:
- **ArUco**: `ArUcoTrackingAppCoordinator` + `ArUcoMarkerTracking` – einzelne Marker, ID→GameObject-Map
- **ChArUco**: `ChArUcoTrackingAppCoordinator` + `ChArUcoMarkerTracking` – Schachbrettboard, ein Zielobjekt

---

## Scripts

| Datei | Aufgabe |
|---|---|
| `Assets/Scripts/ArUcoMarkerTracking.cs` | OpenCV Detection + solvePnP-Pose. `GetDetectedMarkerIds()` gibt aktuelle IDs zurück |
| `Assets/Scripts/ArUcoTrackingAppCoordinator.cs` | App-Logik, Kamera-Init, Spatial Anchor Management inkl. Refresh |
| `Assets/Scripts/ChArUcoMarkerTracking.cs` | OpenCV Detection + solvePnP für ChArUco-Boards |
| `Assets/Scripts/ChArUcoTrackingAppCoordinator.cs` | App-Logik für ChArUco |
| `Assets/Scripts/CameraImageAduster.cs` | Passt Quad-Skalierung an Kameraintrinsics an |
| `Assets/Editor/MetaXRManifestWorkaround.cs` | Build-Fix: löscht stale Gradle-Artefakte vor Meta SDK Callback |

---

## Key Dependencies

| Package | Version | Zweck |
|---|---|---|
| `com.meta.xr.sdk.all` | 85.0.0 | Meta Quest SDK (PassthroughCameraAccess, OVRSpatialAnchor, OVRInput) |
| OpenCV for Unity | – | Asset in `Assets/OpenCVForUnity/` (nicht im manifest.json) |
| `com.unity.xr.openxr` | 1.16.1 | XR Foundation |
| `com.unity.xr.meta-openxr` | 2.4.0 | Meta OpenXR Extension |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP |
| `com.ivanmurzak.unity.mcp` | 0.54.0 | MCP Unity Bridge |

---

## Wichtige Konzepte

### Kamerakalibrierung
`PassthroughCameraAccess.Intrinsics` liefert `fx, fy, cx, cy` + `SensorResolution`. Falls `CurrentResolution` abweicht, werden Intrinsics skaliert. Distortion ist immer 0 (Quest-Kameras vorverzeichnet).

### Image Pipeline
```
Quest Kamera → Texture → Texture2D → Mat (RGBA)
  → resize (÷ _divideNumber)  → cvtColor RGBA→RGB
  → detectMarkers() → solvePnP()
  → ARUtils.ConvertRvecTvecToPoseData()
  → camTransform.localToWorldMatrix * arMatrix
  → SetTransformFromMatrix(targetObject)   ← nur wenn kein Anchor aktiv
```

### _divideNumber
Default 2 – halbiert Verarbeitungsauflösung. Intrinsics werden entsprechend skaliert.

### Pose-Smoothing
`Vector3.Lerp` / `Quaternion.Slerp` mit `_poseFilterCoefficient` (0–1). Pro Marker-ID in `_prevPoseDataDictionary`.

### Szene
`[BuildingBlock] Spatial Anchor Core` ist bereits in der Szene – Voraussetzung für `OVRSpatialAnchor`.
AR-Objekte (`ARGameObject_0`, `ARGameObject_1`) sind Root-GameObjects; `OVRSpatialAnchor` wird zur Laufzeit per `AddComponent` hinzugefügt.

### ChArUco-Sonderfall
Pose wird nur berechnet wenn `_charucoIds.total() >= 4` (trotz `_charucoMinMarkers = 2`). Rotation wird um 180° um X gedreht (`poseData.rot * Quaternion.Euler(180, 0, 0)`).
