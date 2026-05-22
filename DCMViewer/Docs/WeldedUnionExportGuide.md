# Welded Union STL Export Guide

This guide documents how the welded-union STL export was implemented in DCMViewer so the same change can be reproduced in another WPF app with Copilot.

## Goal

Add a new export mode that outputs one STL where touching meshes are welded together into a connected object, instead of only writing multiple disconnected shells into a single file.

This project now includes an additional third mode for harder cases: force single-component union.

## What was added in this project

### View model command integration

File: MainViewModel.cs

- New command field: ExportWeldedUnionMeshesCommand.
- New command initialization in constructor.
- New command property for XAML binding.
- CanExecute wiring tied to the same availability rules as other export commands.
- New async method ExportWeldedUnionMeshes:
  - gathers visible MeshGeometry3D snapshots
  - prompts for output STL path
  - calls MeshExportService.ExportWeldedUnionStl
  - reports source mesh count, output triangle count, and connected component count

### UI integration

File: MainWindow.xaml

- Added one toolbar button bound to ExportWeldedUnionMeshesCommand.
- Tooltip explicitly states this is a welded union export for touching points.

### Export service algorithm

File: MeshExportService.cs

Added:

- WeldedUnionResult record for summary metrics.
- ExportWeldedUnionStl(filePath, meshes, weldTolerance).
- ExportForceSingleComponentUnionStl(filePath, meshes, weldTolerance).
- Internal helpers:
  - FindOrAddWeldedVertex
  - QuantizePoint
  - ToCanonicalTriangle
  - CountConnectedComponents
  - AddUndirectedEdge

Algorithm details:

1. Validate input meshes and tolerance.
2. Enumerate all triangles from all input meshes.
3. Weld vertices by tolerance using spatial hashing:
	- quantize each vertex to a 3D cell key
	- probe neighboring cells (3x3x3)
	- reuse existing vertex index when distance <= tolerance
4. Build output triangles with welded indices.
5. Drop degenerate triangles (collapsed indices).
6. Remove duplicate triangles using canonical sorted index triplets.
7. Cleanup pass:
	- remove tiny-area triangles (noise fragments)
	- remove tiny disconnected components (very low triangle count)
	- compact vertex/triangle indices
8. Write one binary STL from cleaned welded output.
9. Compute connected component count on the cleaned triangle graph.
10. Return WeldedUnionResult for status display.

### Third mode: force single-component union

If welded union still yields multiple connected components, this mode:

1. Finds disconnected components.
2. Adds small tube bridges between component anchor points.
3. Runs final cleanup and reindexing.
4. Exports one connected-shell STL.

This mode is intentionally geometry-modifying and should be used only when one connected shell is mandatory.

## Chosen defaults

- Weld tolerance: 0.001 model units.
- Output format: binary STL.

Runtime configuration:

- Exposed as MainViewModel.WeldedUnionTolerance.
- Bound in UI as a top-toolbar input labeled Weld tol.
- Clamped range: 0.00001 to 1.0.

Why this default:

- small enough to avoid aggressive shape collapse
- large enough to weld tiny floating-point gaps from scan/export pipelines

## Behavioral expectations

### What this solves

- One output STL file.
- Touching regions can become topologically connected (shared welded vertices).
- Many downstream tools will treat this as one object when connectivity is achieved.

### What this does not solve

- Not full CSG boolean union.
- Does not remove all interior overlap surfaces when triangulations do not match.
- For exact volumetric union, use a dedicated boolean mesh kernel.

## Porting checklist for another app

1. Add a mesh snapshot type if missing:
	- positions array
	- triangle index array
2. Implement tolerance-based vertex weld using spatial hashing.
3. Add duplicate/degenerate triangle filtering.
4. Export the welded mesh as one STL.
5. Add a dedicated UI action separate from existing simple combine export.
6. Report connectivity metrics to users.
7. Keep old combine export mode for compatibility.

## Copilot prompt template for another app

Copy and adapt this prompt:

Implement a new STL export mode called "Welded Union STL" in this WPF app.

Requirements:
- Keep existing export modes unchanged.
- Add a new command and UI button for welded union export.
- Collect all visible meshes and export one STL where touching points are welded.
- Weld vertices using a tolerance-based spatial hash (neighbor cell lookup).
- Remove degenerate triangles after welding.
- Remove duplicate triangles by canonical sorted index triplets.
- Write binary STL output.
- Return and show metrics: source mesh count, output triangle count, connected components, weld tolerance.
- Use tolerance default 0.001 model units.
- Do not implement full CSG boolean logic; document this limitation clearly.

Code organization guidance:
- Put welding/export algorithm in a MeshExportService-style class.
- Keep UI command logic in the main view model.
- Bind a new toolbar button in XAML to the new command.
- Make CanExecute match existing export availability logic.

## Validation steps

Use these tests after implementation:

1. Two identical touching meshes:
	- export welded union
	- verify component count tends toward one
2. Two far-apart meshes:
	- component count should remain two
3. Tolerance sensitivity:
	- smaller tolerance keeps more separation
	- larger tolerance increases weld likelihood
4. Degenerate protection:
	- no invalid triangles in output
5. Regression:
	- original simple combined export still behaves as before

## Notes for maintainers

- If users report over-welding, lower tolerance.
- If users report non-fusing seams, increase tolerance slightly.
- Consider exposing tolerance in UI only if advanced users need control.

In DCMViewer this is already exposed in UI and can also be set programmatically.
