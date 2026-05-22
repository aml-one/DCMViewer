# AmL.DCMViewer Publish Package

## Where To Find Things

- Single-DLL deliverable (merged): `publish/single/AmL.DCMViewer.dll`
- XML doc for IntelliSense (single-DLL package): `publish/single/AmL.DCMViewer.xml`
- DLL: `publish/bin/AmL.DCMViewer.dll`
- XML doc for IntelliSense (standard output): `publish/bin/AmL.DCMViewer.xml`
- Dependency DLLs:
	- `publish/bin/HelixToolkit.Wpf.dll`
	- `publish/bin/BouncyCastle.Cryptography.dll`
- Assembly dependency manifest: `publish/bin/AmL.DCMViewer.deps.json`
- Reusable canvas component source:
	- `publish/component/DcmViewerCanvasComponent.xaml`
	- `publish/component/DcmViewerCanvasComponent.xaml.cs`
- Dictionary documentation: `publish/docs/Dictionaries.md`

## How To Use The DLL In Another WPF App

### Recommended one-DLL use

Use the merged file:

- `publish/single/AmL.DCMViewer.dll`

No separate `HelixToolkit.Wpf.dll` or `BouncyCastle.Cryptography.dll` is required for this merged output.

### Rebuild the merged one-DLL package

From repo root:

One-click script from repo root:

```powershell
.\publish\build-single.ps1
```

For Release build:

```powershell
.\publish\build-single.ps1 -Configuration Release
```

Equivalent direct command:

```powershell
dotnet build .\DCMViewer\DCMViewer.csproj -p:MergeToSingleDll=true -p:OutputPath=.\bin\SingleMerge\ -p:IntermediateOutputPath=.\obj\SingleMerge\
```

The merged output is written to:

- `publish/single/AmL.DCMViewer.dll`

### Standard multi-DLL use (optional)

1. Copy files from `publish/bin` into your host app output (or reference location).
2. Add a reference to `AmL.DCMViewer.dll`.
3. Use the component namespace in XAML:

```xml
xmlns:dcm="clr-namespace:DCMViewer.Controls;assembly=AmL.DCMViewer"
```

4. Place the reusable canvas component:

```xml
<dcm:DcmViewerCanvasComponent x:Name="ViewerCanvas" />
```

5. Bind a `MainViewModel` as DataContext (or equivalent host VM wiring).

### Component customization options

`DcmViewerCanvasComponent` exposes these properties for host customization:

- `UseFullAppShell` (default true; includes full app toolbar/buttons/panels)
- `IsBackgroundTransparent` (when true, component background is transparent)
- `IsWatermarkVisible` (show/hide logo + watermark text)
- `GradientMode`:
	- `Radial`
	- `LinearHorizontal`
	- `LinearVertical`
- `GradientStartColor`
- `GradientMidColor`
- `GradientMidOuterColor`
- `GradientOuterColor`
- `IsLogoVisible` (show/hide logo)
- `LogoSource` (replace logo image)
- `WatermarkText`
- `WatermarkTextColor`
- `WatermarkTextFontSize`

Example:

```xml
<dcm:DcmViewerCanvasComponent
	IsWatermarkVisible="True"
	IsBackgroundTransparent="False"
		GradientMode="LinearHorizontal"
		GradientStartColor="#FFFDFEFE"
		GradientMidColor="#FFF1F4F7"
		GradientMidOuterColor="#FFD7DBDF"
		GradientOuterColor="#FFB8BDC2"
		IsLogoVisible="True"
		WatermarkText="MyLab"
		WatermarkTextColor="#FF9A8748"
		WatermarkTextFontSize="72" />
```

	Fully see-through canvas:

	```xml
	<dcm:DcmViewerCanvasComponent
		IsBackgroundTransparent="True"
		IsWatermarkVisible="False" />
	```

To replace logo from code-behind:

```csharp
ViewerCanvas.LogoSource = new BitmapImage(new Uri(@"C:\assets\my-logo.png"));
```

## Dictionary Usage (Grouping + Texture)

Configure these dictionaries on `MainViewModel` before loading files:

- `TextureOverrides`: filename -> texture name
- `CategoryOverrides`: filename -> category (`model`, `scan`, `restoration`, `abutment`)

Example:

```csharp
var vm = new DCMViewer.ViewModels.MainViewModel(new DCMViewer.Services.DcmParser());

vm.TextureOverrides["scan.dcm"] = "Model";
vm.TextureOverrides["r1.dcm"] = "Zirconia";

vm.CategoryOverrides["scan.dcm"] = "scan";
vm.CategoryOverrides["r1.dcm"] = "restoration";

await vm.LoadFilesAsync(files, clearExisting: false);
```

Valid texture/material names are:

- `Model`
- `Zirconia`
- `Emax`
- `Stone`
- `Gold`
- `SLM`
- `PMMA`
- `WAX`

## STL export behaviors

The viewer offers three STL export paths:

- Export visible scans as separate STL files:
	- Writes one STL per visible mesh.
- Export visible scans as one merged STL:
	- Writes one STL file but only concatenates triangles.
	- Geometry remains multi-shell if inputs are disconnected.
- Export visible scans as welded union STL:
	- Writes one STL file after welding touching vertices (tolerance-based).
	- Designed to convert touching meshes into one connected object.
- Export visible scans as single-component union STL:
	- Starts with welded union cleanup.
	- If components are still disconnected, adds small bridge geometry to force one connected shell.
	- Reports bridge count in status text.

Current welded-union tolerance in the shipped implementation is `0.001` (model units).

Note: welded union is not a full boolean CSG union. It welds touching geometry and removes exact duplicate triangles, but does not compute full solid inside/outside subtraction of overlapping volumes.

Single-component union goes further by adding connector bridges when needed. This is useful for workflows that strictly require one connected shell, but it is geometry-modifying by design.
