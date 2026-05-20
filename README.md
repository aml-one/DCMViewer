# AmL DCMViewer

A desktop WPF viewer for dental 3D data (DCM and STL), with realistic material rendering, section analysis, and measurement tooling.

This repository contains:

- A full desktop viewer app in `DCMViewer/`
- A reusable viewer canvas component for embedding into other WPF apps
- A publish workflow that can produce a merged single-DLL deliverable

## What This Project Is For

AmL DCMViewer is built to inspect and present dental meshes quickly and clearly:

- Open and view `.dcm`, `.stl`, and selected `.xml` workflows
- Apply dental material looks (zirconia, emax, gold, etc.)
- Control grouped visibility/opacity for scan/restoration/abutment-style workflows
- Slice models with a section plane and inspect a 2D cross-section profile
- Measure distances on model space and section space
- Export visible geometry as merged or separate STL files

## Main Capabilities

### 1) File loading and visualization

- Supports drag-and-drop and file picker loading
- Handles encrypted/metadata-rich DCM parsing via custom parser service
- Uses HelixToolkit WPF for 3D camera/navigation and rendering

### 2) Material and texture system

Material choices are managed by `MaterialLibrary` and exposed through UI + runtime overrides.

Available texture/material names:

- `Model`
- `Zirconia`
- `Emax`
- `Stone`
- `Gold`
- `SLM`
- `PMMA`
- `WAX`

### 3) Grouping controls

Supports grouped control patterns (for example restoration/abutment visibility and opacity) while still allowing per-file behavior.

### 4) Section and cross-section analysis

- Interactive section plane placement in 3D
- 2D cross-section profile rendering
- Per-category profile coloring
- Stable grid overlay in section panel
- Cross-section measurements with snapping

### 5) Export

- Export visible meshes as one merged STL
- Export visible meshes as separate STL files

## How It Works (High-Level Architecture)

### UI and interaction

- `DCMViewer/MainWindow.xaml`
- `DCMViewer/MainWindow.xaml.cs`

The main window handles viewport interaction, section plane mechanics, 2D profile drawing, and measurement overlays.

### View model and state

- `DCMViewer/ViewModels/MainViewModel.cs`

Contains core app state, commands, loading pipeline, grouping logic, material resolution, camera state, and export orchestration.

### Services

- `DCMViewer/Services/DcmParser.cs` for parsing and extraction
- `DCMViewer/Services/MaterialLibrary.cs` for named material palettes
- `DCMViewer/Services/SectionGeometryService.cs` for section intersection geometry
- `DCMViewer/Services/MeshExportService.cs` for STL exports

### Reusable canvas component

- `DCMViewer/Controls/DcmViewerCanvasComponent.xaml`
- `DCMViewer/Controls/DcmViewerCanvasComponent.xaml.cs`

This component packages the main viewport/canvas area for reuse in other host applications.

## Dictionary Configuration (Grouping and Materials)

`MainViewModel` exposes two runtime dictionaries:

- `TextureOverrides`: file name -> texture name
- `CategoryOverrides`: file name -> category (`model`, `scan`, `restoration`, `abutment`)

Set these before loading files for deterministic behavior.

Example:

```csharp
var vm = new DCMViewer.ViewModels.MainViewModel(new DCMViewer.Services.DcmParser());

vm.TextureOverrides["scan.dcm"] = "Model";
vm.TextureOverrides["r1.dcm"] = "Zirconia";

vm.CategoryOverrides["scan.dcm"] = "scan";
vm.CategoryOverrides["r1.dcm"] = "restoration";

await vm.LoadFilesAsync(files, clearExisting: false);
```

More details are in:

- `DCMViewer/Docs/Dictionaries.md`

## Build and Run

From repository root:

```powershell
dotnet build .\DCMViewer\DCMViewer.csproj
dotnet run --project .\DCMViewer\DCMViewer.csproj
```

## Output Artifacts

The assembly output name is configured as:

- `AmL.DCMViewer.dll`

Default build output:

- `DCMViewer/bin/Debug/net10.0-windows/AmL.DCMViewer.dll`

## Single-DLL Publish Workflow

This repository includes an optional merge flow (ILRepack) to produce one merged DLL containing app + dependency assemblies.

One-click script:

```powershell
.\publish\build-single.ps1
```

Release variant:

```powershell
.\publish\build-single.ps1 -Configuration Release
```

Merged output:

- `publish/single/AmL.DCMViewer.dll`

Publish package references:

- `publish/README.md`
- `publish/docs/Dictionaries.md`

## Using the Component in Another WPF App

1. Reference `AmL.DCMViewer.dll` in your host app.
2. Import namespace in XAML:

```xml
xmlns:dcm="clr-namespace:DCMViewer.Controls;assembly=AmL.DCMViewer"
```

3. Place the control:

```xml
<dcm:DcmViewerCanvasComponent x:Name="ViewerCanvas" />
```

4. Bind a compatible `MainViewModel` (or adapt your host VM integration).

## Repository Structure

- `DCMViewer/` application project
- `publish/` packaged outputs and usage docs
- `Stuff/` research/non-product material (ignored from git)

## Notes and Troubleshooting

- If build files are locked, close running viewer instances before rebuild.
- HelixToolkit may emit compatibility warning NU1701 with newer target frameworks; verify runtime behavior in your environment.
- Build artifacts (`.vs`, `bin`, `obj`) are ignored and can be cleaned locally.

## License

No explicit license file is currently included in this repository. Add one if you plan external distribution.
