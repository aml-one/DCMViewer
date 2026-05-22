# DCM Viewer Dictionary Configuration

This project exposes two runtime dictionaries on `MainViewModel` to control per-file behavior:

- `TextureOverrides`
- `CategoryOverrides`

Both are keyed by file name only (for example `scan.dcm`), case-insensitive.

## 1) TextureOverrides

Purpose: force a material/texture for specific files.

Type:

- `Dictionary<string, string>`
- Key: file name (e.g. `teeth.dcm`)
- Value: material name from `MaterialLibrary` (e.g. `Zirconia`, `Model`, `Gold`)

Example:

```csharp
viewModel.TextureOverrides["scan.dcm"] = "Model";
viewModel.TextureOverrides["r1.dcm"] = "Zirconia";
```

Resolution order:

1. `TextureOverrides`
2. Category default (restoration files default to `Zirconia`)
3. Metadata heuristics
4. `DefaultTextureName`

## 2) CategoryOverrides

Purpose: force grouping/category for specific files.

Type:

- `Dictionary<string, string>`
- Key: file name (e.g. `r1.dcm`)
- Value: one of:
  - `model`
  - `scan`
  - `restoration`
  - `abutment`

Example:

```csharp
viewModel.CategoryOverrides["scan.dcm"] = "scan";
viewModel.CategoryOverrides["r1.dcm"] = "restoration";
viewModel.CategoryOverrides["a1.dcm"] = "abutment";
```

## 3) When to set dictionaries

Set dictionaries before calling `LoadFileAsync`/`LoadFilesAsync` for deterministic behavior.

```csharp
var viewModel = new MainViewModel(new DcmParser());

viewModel.CategoryOverrides["r1.dcm"] = "restoration";
viewModel.TextureOverrides["r1.dcm"] = "Zirconia";

await viewModel.LoadFilesAsync(files, clearExisting: false);
```

If you update dictionaries after files are loaded, call a refresh path in your host (or reload files) so visuals are rebuilt.

## 4) Available texture names

Bind or read `AvailableTextures`:

```csharp
IReadOnlyList<string> names = viewModel.AvailableTextures;
```

This list is sourced from `MaterialLibrary.Names`.

Current material/texture options are:

- `Model`
- `Zirconia`
- `Emax`
- `Stone`
- `Gold`
- `SLM`
- `PMMA`
- `WAX`

## 5) Disable external drag-and-drop loading

If your host application should not allow users to drag/drop files onto the viewer,
set this flag on `MainViewModel`:

```csharp
viewModel.IsExternalFileDropEnabled = false;
```

When `false`, `.dcm/.stl/.xml` file drops are ignored.

## 6) Reusable component customization options

If you host `DcmViewerCanvasComponent` in another app, you can customize background,
logo, and watermark text through component properties.

- `UseFullAppShell` (default `true`): embeds the full DCMViewer UI including toolbar/buttons/panels.
  Set to `false` for canvas-only mode where the options below apply directly.

### Gradient options

- `IsBackgroundTransparent` (true/false; when true host background shows through)
- `GradientMode`
  - `Radial`
  - `LinearHorizontal`
  - `LinearVertical`
- `GradientStartColor`
- `GradientMidColor`
- `GradientMidOuterColor`
- `GradientOuterColor`

### Logo options

- `IsWatermarkVisible` (show/hide all watermark visuals: logo + text)
- `IsLogoVisible` (true/false)
- `LogoSource` (replace logo image)

### Watermark text options

- `WatermarkText`
- `WatermarkTextColor`
- `WatermarkTextFontSize`

### XAML example

```xml
<dcm:DcmViewerCanvasComponent
  IsWatermarkVisible="True"
  IsBackgroundTransparent="False"
    GradientMode="LinearHorizontal"
    GradientStartColor="#FFFFFFFF"
    GradientMidColor="#FFF3F6F9"
    GradientMidOuterColor="#FFD3D7DA"
    GradientOuterColor="#FFB0B4B8"
    IsLogoVisible="True"
    WatermarkText="AmL"
    WatermarkTextColor="#FFB8A35C"
    WatermarkTextFontSize="80" />
```

  For a fully see-through canvas area:

  ```xml
  <dcm:DcmViewerCanvasComponent
    IsBackgroundTransparent="True"
    IsWatermarkVisible="False" />
  ```

### Replace logo from code

```csharp
using System;
using System.Windows.Media.Imaging;

viewerCanvas.LogoSource = new BitmapImage(new Uri(@"C:\assets\custom-logo.png"));
```

## 7) STL export modes (combined vs welded union)

The viewer now exposes three different export behaviors for visible meshes:

- Separate STL export: one STL per visible mesh.
- Combined STL export: one STL file containing all visible triangles, without geometry fusion.
- Welded union STL export: one STL where vertices from touching meshes are welded by position tolerance.
- Single-component union STL export: welded union plus automatic bridge geometry to connect remaining disconnected shells.

### What welded union does

Welded union is intended for workflows where multiple scans should become one connected object.

- Input: all currently visible meshes.
- Processing:
  - Vertices are merged when they are within the weld tolerance (currently `0.001` in model units).
  - Degenerate triangles created by welding are removed.
  - Duplicate triangles with the same vertex triplet are removed.
- Output: one STL mesh snapshot and a post-export connectivity summary.

### Weld tolerance setting

Welded union uses `MainViewModel.WeldedUnionTolerance`.

- Default: `0.001`
- Allowed range: `0.00001` to `1.0`
- UI: top toolbar field labeled `Weld tol:`

Usage from host code:

```csharp
viewModel.WeldedUnionTolerance = 0.0008;
```

Guidance:

- Lower values reduce accidental over-welding.
- Higher values increase the chance of fusing tiny gaps between touching scans.

The status line reports:

- source mesh count
- output triangle count
- output connected component count
- weld tolerance

### Important limitation

Welded union is not a full constructive-solid-geometry boolean union.

- It does connect touching parts by sharing welded vertices.
- It does not perform volumetric inside/outside classification.
- Internal coincident faces may remain if two parts overlap but do not share exactly matching triangulation.

For scan cleanup and most "touching shells should become one object" scenarios, welded union is typically sufficient.
For exact CAD boolean behavior, use a dedicated boolean mesh kernel.

### Single-component union mode

When welded union still leaves more than one connected component, single-component union adds small bridge tubes
between disconnected components so the result becomes one connected shell for downstream tools.

- Uses the same `WeldedUnionTolerance` setting.
- Reports `bridges` count in status text.
- Intended for manufacturing/export pipelines that require one connected object.

Note: this mode intentionally modifies geometry by adding connector bridges. Use welded union mode when you must avoid any added geometry.
