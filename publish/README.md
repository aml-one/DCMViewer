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
