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

### Gradient options

- `GradientMode`
  - `Radial`
  - `LinearHorizontal`
  - `LinearVertical`
- `GradientStartColor`
- `GradientMidColor`
- `GradientMidOuterColor`
- `GradientOuterColor`

### Logo options

- `IsLogoVisible` (true/false)
- `LogoSource` (replace logo image)

### Watermark text options

- `WatermarkText`
- `WatermarkTextColor`
- `WatermarkTextFontSize`

### XAML example

```xml
<dcm:DcmViewerCanvasComponent
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

### Replace logo from code

```csharp
using System;
using System.Windows.Media.Imaging;

viewerCanvas.LogoSource = new BitmapImage(new Uri(@"C:\assets\custom-logo.png"));
```
