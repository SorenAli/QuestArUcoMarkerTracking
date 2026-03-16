# QuestArUcoMarkerTracking – Claude Code Context

## Projektübersicht

Unity-AR-Anwendung für Meta Quest, die ArUco-Marker per OpenCV erkennt und 3D-GameObjects in Echtzeit an Markerpositionen platziert. Nutzt die Passthrough-Kamera der Quest, um Marker im physischen Raum zu tracken.

**Namespace:** `TryAR.MarkerTracking`
**Hauptszene:** `Assets/Scenes/SampleScene.unity`
**Build-Ziel:** Android (Meta Quest)

---

## Architektur

```
PassthroughCameraAccess (Meta XR SDK)
        │
        ▼
AppCoordinator (MonoBehaviour)
  ├── InitializeMarkerTracking()  → liest Kamerakalibrierung, initialisiert Tracker
  ├── Update()
  │     ├── UpdateCameraPoses()   → setzt CameraAnchor-Transform
  │     └── ProcessMarkerTracking()
  │           ├── DetectMarker()  → OpenCV Detection
  │           └── EstimatePose()  → solvePnP → GameObject positionieren
  └── HandleVisualizationToggle() → OVR Button.One togglet Debug-View
```

Zwei parallele Varianten:
- **Standard ArUco**: `ArUcoTrackingAppCoordinator` + `ArUcoMarkerTracking` – einzelne Marker, ID→GameObject-Mapping
- **ChArUco**: `ChArUcoTrackingAppCoordinator` + `ChArUcoMarkerTracking` – Schachbrett-Marker, einzelnes Zielobjekt

---

## Scripts (`Assets/Scripts/`)

| Datei | Klasse | Aufgabe |
|---|---|---|
| [ArUcoMarkerTracking.cs](Assets/Scripts/ArUcoMarkerTracking.cs) | `ArUcoMarkerTracking` | OpenCV Detection + PnP-Pose für einzelne Marker |
| [ArUcoTrackingAppCoordinator.cs](Assets/Scripts/ArUcoTrackingAppCoordinator.cs) | `ArUcoTrackingAppCoordinator` | App-Logik, Kamera-Init, Update-Loop für ArUco |
| [ChArUcoMarkerTracking.cs](Assets/Scripts/ChArUcoMarkerTracking.cs) | `ChArUcoMarkerTracking` | OpenCV Detection + PnP-Pose für ChArUco-Boards |
| [ChArUcoTrackingAppCoordinator.cs](Assets/Scripts/ChArUcoTrackingAppCoordinator.cs) | `ChArUcoTrackingAppCoordinator` | App-Logik für ChArUco |
| [CameraImageAduster.cs](Assets/Scripts/CameraImageAduster.cs) | `CameraImageAduster` | Passt Quad-Skalierung an Kameraintrinsics an |

---

## Key Dependencies

| Package | Version | Zweck |
|---|---|---|
| `com.meta.xr.sdk.all` | 85.0.0 | Meta Quest SDK (PassthroughCameraAccess, OVRInput) |
| OpenCV for Unity | – | Asset (nicht im manifest), in `Assets/OpenCVForUnity/` |
| `com.unity.xr.openxr` | 1.16.1 | XR Foundation |
| `com.unity.xr.meta-openxr` | 2.4.0 | Meta OpenXR Extension |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP |
| `com.ivanmurzak.unity.mcp` | 0.54.0 | MCP Unity Bridge (Claude Code Integration) |

---

## Wichtige Konzepte

### Kamerakalibrierung
`PassthroughCameraAccess.Intrinsics` liefert `fx, fy, cx, cy` und `SensorResolution`. Falls `CurrentResolution` abweicht, werden die Intrinsics skaliert. Distortion ist immer 0 (Quest-Kameras sind bereits entzerrt).

### Image Pipeline
```
Quest Kamera → Texture → Texture2D → OpenCV Mat (RGBA)
  → resize (÷ divideNumber)
  → cvtColor (RGBA→RGB)
  → detectMarkers()
  → solvePnP()
  → ARUtils.ConvertRvecTvecToPoseData()
  → camTransform.localToWorldMatrix * arMatrix
  → SetTransformFromMatrix(targetObject)
```

### Pose-Smoothing
Low-Pass Filter via `Vector3.Lerp` / `Quaternion.Slerp` mit `_poseFilterCoefficient` (0–1, höher = mehr Glättung). Gespeichert pro Marker-ID in `_prevPoseDataDictionary`.

### DivideNumber
`_divideNumber` (default 2) halbiert die Verarbeitungsauflösung. Intrinsics werden entsprechend skaliert. Höher = schneller, aber ungenauer.

### Visualisierungs-Toggle
OVR Button.One togglet zwischen Debug-Camera-View (`m_debugRenderer` mit `resultTexture`) und AR-Objekt-Ansicht.

---

## Marker-Setup (ArUco)

In `ArUcoTrackingAppCoordinator` im Inspector:
- `m_markerGameObjectPairs`: Liste von `{markerId (int), gameObject}` – mappt Marker-IDs auf 3D-Objekte
- `_dictionaryId`: ArUco-Dictionary (default `DICT_4X4_50`)
- `_markerLength`: physische Markergröße in Metern (default 0.1m)

---

## ChArUco-Board-Setup

- `_squaresX` / `_squaresY`: Board-Größe in Feldern
- `_squareLength`: Schachbrettfeld-Größe in Metern
- `_markerLength`: ArUco-Marker-Größe innerhalb des Boards
- `_charucoMinMarkers`: Mindestanzahl erkannter Marker für Pose (default 2, effektiv 4 wegen `_charucoIds.total() < 4`)

---

## Build

- Platform: Android
- Target: Meta Quest (OpenXR + Meta XR SDK)
- Permissions: Passthrough Camera muss in den Quest App-Einstellungen aktiviert sein
