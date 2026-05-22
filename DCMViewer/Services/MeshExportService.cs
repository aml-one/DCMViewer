using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Media3D;

namespace DCMViewer.Services;

internal static class MeshExportService
{
    internal sealed record MeshSnapshot(Point3D[] Positions, int[] TriangleIndices);
    internal sealed record MeshExportItem(string DisplayName, MeshSnapshot Mesh);
    internal sealed record WeldedUnionResult(
        int SourceMeshCount,
        int SourceTriangleCount,
        int OutputVertexCount,
        int OutputTriangleCount,
        int OutputConnectedComponents,
        int RemovedTriangleCount,
        int RemovedSmallComponentCount,
        int AddedBridgeCount);

    private sealed record MeshCleanupResult(
        Point3D[] Vertices,
        int[] TriangleIndices,
        List<(int I0, int I1, int I2)> Triangles,
        int RemovedTriangleCount,
        int RemovedSmallComponentCount);

    public static MeshSnapshot CreateSnapshot(MeshGeometry3D mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        return new MeshSnapshot(
            mesh.Positions.ToArray(),
            mesh.TriangleIndices.ToArray());
    }

    public static void Export(string filePath, IReadOnlyList<MeshSnapshot> meshes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(meshes);

        if (meshes.Count == 0)
        {
            throw new InvalidOperationException("No mesh data is available to export.");
        }

        var extension = Path.GetExtension(filePath);
        if (string.Equals(extension, ".stl", StringComparison.OrdinalIgnoreCase))
        {
            WriteBinaryStl(filePath, meshes);
            return;
        }

        if (string.Equals(extension, ".ply", StringComparison.OrdinalIgnoreCase))
        {
            WriteAsciiPly(filePath, meshes);
            return;
        }

        throw new NotSupportedException($"Unsupported export format '{extension}'.");
    }

    public static void ExportSeparateStl(string baseFilePath, IReadOnlyList<MeshExportItem> meshes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFilePath);
        ArgumentNullException.ThrowIfNull(meshes);

        if (meshes.Count == 0)
        {
            throw new InvalidOperationException("No mesh data is available to export.");
        }

        var directory = Path.GetDirectoryName(baseFilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.CurrentDirectory;
        }

        var baseName = Path.GetFileNameWithoutExtension(baseFilePath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "visible-meshes";
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in meshes)
        {
            var safeName = MakeSafeFileName(Path.GetFileNameWithoutExtension(item.DisplayName));
            if (string.IsNullOrWhiteSpace(safeName))
            {
                safeName = "mesh";
            }

            var fileNameBase = $"{baseName}_{safeName}";
            var uniqueFileNameBase = fileNameBase;
            var suffix = 2;
            while (!usedNames.Add(uniqueFileNameBase))
            {
                uniqueFileNameBase = $"{fileNameBase}_{suffix++}";
            }

            var outputPath = Path.Combine(directory, uniqueFileNameBase + ".stl");
            WriteBinaryStl(outputPath, new[] { item.Mesh });
        }
    }

    public static WeldedUnionResult ExportWeldedUnionStl(string filePath, IReadOnlyList<MeshSnapshot> meshes, double weldTolerance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(meshes);

        if (meshes.Count == 0)
        {
            throw new InvalidOperationException("No mesh data is available to export.");
        }

        if (weldTolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weldTolerance), "Weld tolerance must be greater than zero.");
        }

        var sourceTriangles = meshes.Sum(GetTriangleCount);
        if (sourceTriangles <= 0)
        {
            throw new InvalidOperationException("The selected meshes do not contain any triangles.");
        }

        var outputVertices = new List<Point3D>();
        var weldedTriangles = new List<(int I0, int I1, int I2)>(sourceTriangles);
        var spatialHash = new Dictionary<(long X, long Y, long Z), List<int>>();
        var uniqueTriangles = new HashSet<(int A, int B, int C)>();

        foreach (var mesh in meshes)
        {
            foreach (var (i0, i1, i2) in EnumerateTriangleIndices(mesh))
            {
                if (i0 < 0 || i1 < 0 || i2 < 0 ||
                    i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
                {
                    continue;
                }

                var w0 = FindOrAddWeldedVertex(mesh.Positions[i0], weldTolerance, outputVertices, spatialHash);
                var w1 = FindOrAddWeldedVertex(mesh.Positions[i1], weldTolerance, outputVertices, spatialHash);
                var w2 = FindOrAddWeldedVertex(mesh.Positions[i2], weldTolerance, outputVertices, spatialHash);

                if (w0 == w1 || w1 == w2 || w2 == w0)
                {
                    continue;
                }

                var canonical = ToCanonicalTriangle(w0, w1, w2);
                if (!uniqueTriangles.Add(canonical))
                {
                    continue;
                }

                weldedTriangles.Add((w0, w1, w2));
            }
        }

        if (weldedTriangles.Count == 0)
        {
            throw new InvalidOperationException("Welded union produced no valid triangles. Try a smaller tolerance.");
        }

        var minTriangleArea = Math.Max((weldTolerance * weldTolerance) * 0.005, 1e-14);
        const int minTrianglesPerComponent = 4;
        var cleanup = CleanupWeldedTriangles(outputVertices, weldedTriangles, minTriangleArea, minTrianglesPerComponent);

        if (cleanup.Triangles.Count == 0)
        {
            throw new InvalidOperationException("Welded union produced no valid triangles after cleanup. Try a smaller tolerance.");
        }

        var resultSnapshot = new MeshSnapshot(cleanup.Vertices, cleanup.TriangleIndices);
        WriteBinaryStl(filePath, new[] { resultSnapshot });

        var connectedComponents = CountConnectedComponents(cleanup.Vertices.Length, cleanup.Triangles);
        return new WeldedUnionResult(
            SourceMeshCount: meshes.Count,
            SourceTriangleCount: sourceTriangles,
            OutputVertexCount: cleanup.Vertices.Length,
            OutputTriangleCount: cleanup.Triangles.Count,
            OutputConnectedComponents: connectedComponents,
            RemovedTriangleCount: cleanup.RemovedTriangleCount,
            RemovedSmallComponentCount: cleanup.RemovedSmallComponentCount,
            AddedBridgeCount: 0);
    }

    public static WeldedUnionResult ExportForceSingleComponentUnionStl(string filePath, IReadOnlyList<MeshSnapshot> meshes, double weldTolerance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(meshes);

        if (meshes.Count == 0)
        {
            throw new InvalidOperationException("No mesh data is available to export.");
        }

        if (weldTolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weldTolerance), "Weld tolerance must be greater than zero.");
        }

        var sourceTriangles = meshes.Sum(GetTriangleCount);
        if (sourceTriangles <= 0)
        {
            throw new InvalidOperationException("The selected meshes do not contain any triangles.");
        }

        var outputVertices = new List<Point3D>();
        var weldedTriangles = new List<(int I0, int I1, int I2)>(sourceTriangles);
        var spatialHash = new Dictionary<(long X, long Y, long Z), List<int>>();
        var uniqueTriangles = new HashSet<(int A, int B, int C)>();

        foreach (var mesh in meshes)
        {
            foreach (var (i0, i1, i2) in EnumerateTriangleIndices(mesh))
            {
                if (i0 < 0 || i1 < 0 || i2 < 0 ||
                    i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
                {
                    continue;
                }

                var w0 = FindOrAddWeldedVertex(mesh.Positions[i0], weldTolerance, outputVertices, spatialHash);
                var w1 = FindOrAddWeldedVertex(mesh.Positions[i1], weldTolerance, outputVertices, spatialHash);
                var w2 = FindOrAddWeldedVertex(mesh.Positions[i2], weldTolerance, outputVertices, spatialHash);

                if (w0 == w1 || w1 == w2 || w2 == w0)
                {
                    continue;
                }

                var canonical = ToCanonicalTriangle(w0, w1, w2);
                if (!uniqueTriangles.Add(canonical))
                {
                    continue;
                }

                weldedTriangles.Add((w0, w1, w2));
            }
        }

        if (weldedTriangles.Count == 0)
        {
            throw new InvalidOperationException("Union export produced no valid triangles. Try a smaller tolerance.");
        }

        var minTriangleArea = Math.Max((weldTolerance * weldTolerance) * 0.005, 1e-14);
        const int minTrianglesPerComponent = 4;
        var cleanup = CleanupWeldedTriangles(outputVertices, weldedTriangles, minTriangleArea, minTrianglesPerComponent);
        if (cleanup.Triangles.Count == 0)
        {
            throw new InvalidOperationException("Union export produced no valid triangles after cleanup. Try a smaller tolerance.");
        }

        var mutableVertices = cleanup.Vertices.ToList();
        var mutableTriangles = cleanup.Triangles.ToList();
        var bridgeCount = ForceSingleComponentByBridging(mutableVertices, mutableTriangles, weldTolerance);

        var finalCleanup = CleanupWeldedTriangles(mutableVertices, mutableTriangles, minTriangleArea, 1);
        if (finalCleanup.Triangles.Count == 0)
        {
            throw new InvalidOperationException("Union export produced no triangles after final bridge cleanup.");
        }

        var finalSnapshot = new MeshSnapshot(finalCleanup.Vertices, finalCleanup.TriangleIndices);
        WriteBinaryStl(filePath, new[] { finalSnapshot });

        var connectedComponents = CountConnectedComponents(finalCleanup.Vertices.Length, finalCleanup.Triangles);
        return new WeldedUnionResult(
            SourceMeshCount: meshes.Count,
            SourceTriangleCount: sourceTriangles,
            OutputVertexCount: finalCleanup.Vertices.Length,
            OutputTriangleCount: finalCleanup.Triangles.Count,
            OutputConnectedComponents: connectedComponents,
            RemovedTriangleCount: cleanup.RemovedTriangleCount + finalCleanup.RemovedTriangleCount,
            RemovedSmallComponentCount: cleanup.RemovedSmallComponentCount + finalCleanup.RemovedSmallComponentCount,
            AddedBridgeCount: bridgeCount);
    }

    private static void WriteBinaryStl(string filePath, IReadOnlyList<MeshSnapshot> meshes)
    {
        var triangleCount = meshes.Sum(GetTriangleCount);
        if (triangleCount <= 0)
        {
            throw new InvalidOperationException("The selected meshes do not contain any triangles.");
        }

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        var header = new byte[80];
        Encoding.ASCII.GetBytes("DCMViewer STL export", 0, "DCMViewer STL export".Length, header, 0);
        writer.Write(header);
        writer.Write((uint)triangleCount);

        foreach (var mesh in meshes)
        {
            foreach (var triangle in EnumerateTriangles(mesh))
            {
                var normal = ComputeNormal(triangle.P0, triangle.P1, triangle.P2);
                WriteVector(writer, normal);
                WritePoint(writer, triangle.P0);
                WritePoint(writer, triangle.P1);
                WritePoint(writer, triangle.P2);
                writer.Write((ushort)0);
            }
        }
    }

    private static void WriteAsciiPly(string filePath, IReadOnlyList<MeshSnapshot> meshes)
    {
        var totalVertices = meshes.Sum(mesh => mesh.Positions.Length);
        var totalFaces = meshes.Sum(GetTriangleCount);
        if (totalFaces <= 0)
        {
            throw new InvalidOperationException("The selected meshes do not contain any triangles.");
        }

        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
        writer.WriteLine("ply");
        writer.WriteLine("format ascii 1.0");
        writer.WriteLine("comment Generated by DCMViewer");
        writer.WriteLine($"element vertex {totalVertices}");
        writer.WriteLine("property float x");
        writer.WriteLine("property float y");
        writer.WriteLine("property float z");
        writer.WriteLine($"element face {totalFaces}");
        writer.WriteLine("property list uchar int vertex_indices");
        writer.WriteLine("end_header");

        foreach (var mesh in meshes)
        {
            foreach (var position in mesh.Positions)
            {
                writer.WriteLine($"{Format(position.X)} {Format(position.Y)} {Format(position.Z)}");
            }
        }

        var vertexOffset = 0;
        foreach (var mesh in meshes)
        {
            foreach (var (i0, i1, i2) in EnumerateTriangleIndices(mesh))
            {
                writer.WriteLine($"3 {vertexOffset + i0} {vertexOffset + i1} {vertexOffset + i2}");
            }

            vertexOffset += mesh.Positions.Length;
        }
    }

    private static int GetTriangleCount(MeshSnapshot mesh)
    {
        if (mesh.TriangleIndices.Length >= 3)
        {
            return mesh.TriangleIndices.Length / 3;
        }

        return mesh.Positions.Length / 3;
    }

    private static IEnumerable<(Point3D P0, Point3D P1, Point3D P2)> EnumerateTriangles(MeshSnapshot mesh)
    {
        foreach (var (i0, i1, i2) in EnumerateTriangleIndices(mesh))
        {
            if (i0 < 0 || i1 < 0 || i2 < 0 ||
                i0 >= mesh.Positions.Length || i1 >= mesh.Positions.Length || i2 >= mesh.Positions.Length)
            {
                continue;
            }

            yield return (mesh.Positions[i0], mesh.Positions[i1], mesh.Positions[i2]);
        }
    }

    private static IEnumerable<(int I0, int I1, int I2)> EnumerateTriangleIndices(MeshSnapshot mesh)
    {
        if (mesh.TriangleIndices.Length >= 3)
        {
            for (var index = 0; index + 2 < mesh.TriangleIndices.Length; index += 3)
            {
                yield return (mesh.TriangleIndices[index], mesh.TriangleIndices[index + 1], mesh.TriangleIndices[index + 2]);
            }

            yield break;
        }

        for (var index = 0; index + 2 < mesh.Positions.Length; index += 3)
        {
            yield return (index, index + 1, index + 2);
        }
    }

    private static Vector3D ComputeNormal(Point3D p0, Point3D p1, Point3D p2)
    {
        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        var normal = Vector3D.CrossProduct(edge1, edge2);
        if (normal.LengthSquared > 1e-12)
        {
            normal.Normalize();
            return normal;
        }

        return new Vector3D(0, 0, 0);
    }

    private static void WriteVector(BinaryWriter writer, Vector3D vector)
    {
        writer.Write((float)vector.X);
        writer.Write((float)vector.Y);
        writer.Write((float)vector.Z);
    }

    private static void WritePoint(BinaryWriter writer, Point3D point)
    {
        writer.Write((float)point.X);
        writer.Write((float)point.Y);
        writer.Write((float)point.Z);
    }

    private static string Format(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private static string MakeSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        return builder.ToString().Trim();
    }

    private static int FindOrAddWeldedVertex(
        Point3D point,
        double tolerance,
        List<Point3D> vertices,
        Dictionary<(long X, long Y, long Z), List<int>> spatialHash)
    {
        var key = QuantizePoint(point, tolerance);
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    var neighbor = (key.X + dx, key.Y + dy, key.Z + dz);
                    if (!spatialHash.TryGetValue(neighbor, out var candidates))
                    {
                        continue;
                    }

                    foreach (var candidateIndex in candidates)
                    {
                        if ((vertices[candidateIndex] - point).Length <= tolerance)
                        {
                            return candidateIndex;
                        }
                    }
                }
            }
        }

        var newIndex = vertices.Count;
        vertices.Add(point);
        if (!spatialHash.TryGetValue(key, out var bucket))
        {
            bucket = new List<int>();
            spatialHash[key] = bucket;
        }

        bucket.Add(newIndex);
        return newIndex;
    }

    private static (long X, long Y, long Z) QuantizePoint(Point3D point, double tolerance)
        => (
            X: (long)Math.Round(point.X / tolerance),
            Y: (long)Math.Round(point.Y / tolerance),
            Z: (long)Math.Round(point.Z / tolerance));

    private static (int A, int B, int C) ToCanonicalTriangle(int i0, int i1, int i2)
    {
        if (i0 > i1)
        {
            (i0, i1) = (i1, i0);
        }

        if (i1 > i2)
        {
            (i1, i2) = (i2, i1);
        }

        if (i0 > i1)
        {
            (i0, i1) = (i1, i0);
        }

        return (i0, i1, i2);
    }

    private static int CountConnectedComponents(int vertexCount, IReadOnlyList<(int I0, int I1, int I2)> triangles)
    {
        if (vertexCount <= 0 || triangles.Count == 0)
        {
            return 0;
        }

        var adjacency = new List<int>[vertexCount];
        var vertexUsed = new bool[vertexCount];
        foreach (var (i0, i1, i2) in triangles)
        {
            vertexUsed[i0] = true;
            vertexUsed[i1] = true;
            vertexUsed[i2] = true;
            AddUndirectedEdge(adjacency, i0, i1);
            AddUndirectedEdge(adjacency, i1, i2);
            AddUndirectedEdge(adjacency, i2, i0);
        }

        var visited = new bool[vertexCount];
        var queue = new Queue<int>();
        var components = 0;

        for (var v = 0; v < vertexCount; v++)
        {
            if (!vertexUsed[v] || visited[v])
            {
                continue;
            }

            components++;
            visited[v] = true;
            queue.Enqueue(v);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var neighbors = adjacency[current];
                if (neighbors is null)
                {
                    continue;
                }

                foreach (var neighbor in neighbors)
                {
                    if (visited[neighbor])
                    {
                        continue;
                    }

                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return components;
    }

    private static void AddUndirectedEdge(List<int>[] adjacency, int a, int b)
    {
        adjacency[a] ??= new List<int>();
        adjacency[b] ??= new List<int>();
        adjacency[a].Add(b);
        adjacency[b].Add(a);
    }

    private static MeshCleanupResult CleanupWeldedTriangles(
        IReadOnlyList<Point3D> vertices,
        IReadOnlyList<(int I0, int I1, int I2)> triangles,
        double minTriangleArea,
        int minTrianglesPerComponent)
    {
        var areaFiltered = new List<(int I0, int I1, int I2)>(triangles.Count);
        var removedSmallAreaTriangles = 0;

        foreach (var triangle in triangles)
        {
            var area = ComputeTriangleArea(vertices[triangle.I0], vertices[triangle.I1], vertices[triangle.I2]);
            if (area <= minTriangleArea)
            {
                removedSmallAreaTriangles++;
                continue;
            }

            areaFiltered.Add(triangle);
        }

        if (areaFiltered.Count == 0)
        {
            return new MeshCleanupResult(Array.Empty<Point3D>(), Array.Empty<int>(), new List<(int I0, int I1, int I2)>(), removedSmallAreaTriangles, 0);
        }

        var vertexToTriangles = new List<int>[vertices.Count];
        for (var triangleIndex = 0; triangleIndex < areaFiltered.Count; triangleIndex++)
        {
            var triangle = areaFiltered[triangleIndex];
            (vertexToTriangles[triangle.I0] ??= new List<int>()).Add(triangleIndex);
            (vertexToTriangles[triangle.I1] ??= new List<int>()).Add(triangleIndex);
            (vertexToTriangles[triangle.I2] ??= new List<int>()).Add(triangleIndex);
        }

        var triangleVisited = new bool[areaFiltered.Count];
        var queue = new Queue<int>();
        var components = new List<List<int>>();

        for (var triangleIndex = 0; triangleIndex < areaFiltered.Count; triangleIndex++)
        {
            if (triangleVisited[triangleIndex])
            {
                continue;
            }

            var component = new List<int>();
            triangleVisited[triangleIndex] = true;
            queue.Enqueue(triangleIndex);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                var triangle = areaFiltered[current];

                EnqueueTriangleNeighbors(triangle.I0, vertexToTriangles, triangleVisited, queue);
                EnqueueTriangleNeighbors(triangle.I1, vertexToTriangles, triangleVisited, queue);
                EnqueueTriangleNeighbors(triangle.I2, vertexToTriangles, triangleVisited, queue);
            }

            components.Add(component);
        }

        var keptTriangleIndices = new HashSet<int>();
        var removedSmallComponents = 0;
        var removedByComponentSize = 0;

        foreach (var component in components)
        {
            if (component.Count >= minTrianglesPerComponent)
            {
                foreach (var triangleIndex in component)
                {
                    keptTriangleIndices.Add(triangleIndex);
                }

                continue;
            }

            removedSmallComponents++;
            removedByComponentSize += component.Count;
        }

        if (keptTriangleIndices.Count == 0)
        {
            var largest = components.OrderByDescending(component => component.Count).First();
            foreach (var triangleIndex in largest)
            {
                keptTriangleIndices.Add(triangleIndex);
            }

            removedSmallComponents = Math.Max(components.Count - 1, 0);
            removedByComponentSize = areaFiltered.Count - keptTriangleIndices.Count;
        }

        var remap = new Dictionary<int, int>();
        var compactVertices = new List<Point3D>();
        var compactTriangles = new List<(int I0, int I1, int I2)>();

        for (var triangleIndex = 0; triangleIndex < areaFiltered.Count; triangleIndex++)
        {
            if (!keptTriangleIndices.Contains(triangleIndex))
            {
                continue;
            }

            var triangle = areaFiltered[triangleIndex];
            var a = RemapVertex(triangle.I0, vertices, remap, compactVertices);
            var b = RemapVertex(triangle.I1, vertices, remap, compactVertices);
            var c = RemapVertex(triangle.I2, vertices, remap, compactVertices);
            compactTriangles.Add((a, b, c));
        }

        var compactTriangleIndices = new int[compactTriangles.Count * 3];
        for (var i = 0; i < compactTriangles.Count; i++)
        {
            var triangle = compactTriangles[i];
            var baseIndex = i * 3;
            compactTriangleIndices[baseIndex] = triangle.I0;
            compactTriangleIndices[baseIndex + 1] = triangle.I1;
            compactTriangleIndices[baseIndex + 2] = triangle.I2;
        }

        var removedTotal = removedSmallAreaTriangles + removedByComponentSize;
        return new MeshCleanupResult(
            compactVertices.ToArray(),
            compactTriangleIndices,
            compactTriangles,
            removedTotal,
            removedSmallComponents);
    }

    private static double ComputeTriangleArea(Point3D p0, Point3D p1, Point3D p2)
    {
        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        return 0.5 * Vector3D.CrossProduct(edge1, edge2).Length;
    }

    private static void EnqueueTriangleNeighbors(int vertexIndex, List<int>[] vertexToTriangles, bool[] visited, Queue<int> queue)
    {
        var neighbors = vertexToTriangles[vertexIndex];
        if (neighbors is null)
        {
            return;
        }

        foreach (var neighbor in neighbors)
        {
            if (visited[neighbor])
            {
                continue;
            }

            visited[neighbor] = true;
            queue.Enqueue(neighbor);
        }
    }

    private static int RemapVertex(int sourceIndex, IReadOnlyList<Point3D> sourceVertices, Dictionary<int, int> remap, List<Point3D> compactVertices)
    {
        if (remap.TryGetValue(sourceIndex, out var mapped))
        {
            return mapped;
        }

        var nextIndex = compactVertices.Count;
        compactVertices.Add(sourceVertices[sourceIndex]);
        remap[sourceIndex] = nextIndex;
        return nextIndex;
    }

    private static int ForceSingleComponentByBridging(List<Point3D> vertices, List<(int I0, int I1, int I2)> triangles, double weldTolerance)
    {
        var components = GetVertexComponents(vertices.Count, triangles);
        if (components.Count <= 1)
        {
            return 0;
        }

        var baseComponent = components.OrderByDescending(component => component.Count).First();
        var mergedVertices = new HashSet<int>(baseComponent);
        var bridgesAdded = 0;

        var radius = Math.Max(weldTolerance * 2.5, GetBoundsDiagonal(vertices) * 0.0006);

        foreach (var component in components)
        {
            if (ReferenceEquals(component, baseComponent))
            {
                continue;
            }

            var anchorA = FindClosestVertexToCentroid(mergedVertices, vertices, component);
            var anchorB = FindClosestVertexToCentroid(component, vertices, mergedVertices);
            if (anchorA < 0 || anchorB < 0 || anchorA == anchorB)
            {
                continue;
            }

            if (AddBridgeTube(vertices, triangles, vertices[anchorA], vertices[anchorB], radius))
            {
                bridgesAdded++;
                foreach (var vertex in component)
                {
                    mergedVertices.Add(vertex);
                }
            }
        }

        return bridgesAdded;
    }

    private static List<HashSet<int>> GetVertexComponents(int vertexCount, IReadOnlyList<(int I0, int I1, int I2)> triangles)
    {
        if (vertexCount <= 0 || triangles.Count == 0)
        {
            return new List<HashSet<int>>();
        }

        var adjacency = new List<int>[vertexCount];
        var used = new bool[vertexCount];
        foreach (var (i0, i1, i2) in triangles)
        {
            used[i0] = true;
            used[i1] = true;
            used[i2] = true;
            AddUndirectedEdge(adjacency, i0, i1);
            AddUndirectedEdge(adjacency, i1, i2);
            AddUndirectedEdge(adjacency, i2, i0);
        }

        var components = new List<HashSet<int>>();
        var visited = new bool[vertexCount];
        var queue = new Queue<int>();
        for (var i = 0; i < vertexCount; i++)
        {
            if (!used[i] || visited[i])
            {
                continue;
            }

            var component = new HashSet<int>();
            visited[i] = true;
            queue.Enqueue(i);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                var neighbors = adjacency[current];
                if (neighbors is null)
                {
                    continue;
                }

                foreach (var neighbor in neighbors)
                {
                    if (visited[neighbor])
                    {
                        continue;
                    }

                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }

            components.Add(component);
        }

        return components;
    }

    private static int FindClosestVertexToCentroid(HashSet<int> sourceVertices, IReadOnlyList<Point3D> allVertices, HashSet<int> centroidFromVertices)
    {
        if (sourceVertices.Count == 0 || centroidFromVertices.Count == 0)
        {
            return -1;
        }

        var centroid = ComputeCentroid(centroidFromVertices, allVertices);
        var best = -1;
        var bestDistance = double.MaxValue;
        foreach (var index in sourceVertices)
        {
            var distance = (allVertices[index] - centroid).LengthSquared;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = index;
            }
        }

        return best;
    }

    private static Point3D ComputeCentroid(HashSet<int> vertices, IReadOnlyList<Point3D> allVertices)
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;
        foreach (var index in vertices)
        {
            var point = allVertices[index];
            x += point.X;
            y += point.Y;
            z += point.Z;
        }

        var inv = 1.0 / vertices.Count;
        return new Point3D(x * inv, y * inv, z * inv);
    }

    private static bool AddBridgeTube(List<Point3D> vertices, List<(int I0, int I1, int I2)> triangles, Point3D a, Point3D b, double radius)
    {
        var axis = b - a;
        if (axis.Length <= 1e-9)
        {
            return false;
        }

        axis.Normalize();
        var reference = Math.Abs(Vector3D.DotProduct(axis, new Vector3D(0, 1, 0))) > 0.92
            ? new Vector3D(1, 0, 0)
            : new Vector3D(0, 1, 0);
        var u = Vector3D.CrossProduct(axis, reference);
        if (u.Length <= 1e-9)
        {
            return false;
        }

        u.Normalize();
        var v = Vector3D.CrossProduct(axis, u);
        v.Normalize();

        const int segments = 6;
        var ringAStart = vertices.Count;
        for (var i = 0; i < segments; i++)
        {
            var angle = (Math.PI * 2.0 * i) / segments;
            var offset = (u * Math.Cos(angle) + v * Math.Sin(angle)) * radius;
            vertices.Add(a + offset);
        }

        var ringBStart = vertices.Count;
        for (var i = 0; i < segments; i++)
        {
            var angle = (Math.PI * 2.0 * i) / segments;
            var offset = (u * Math.Cos(angle) + v * Math.Sin(angle)) * radius;
            vertices.Add(b + offset);
        }

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var a0 = ringAStart + i;
            var a1 = ringAStart + next;
            var b0 = ringBStart + i;
            var b1 = ringBStart + next;
            triangles.Add((a0, a1, b0));
            triangles.Add((a1, b1, b0));
        }

        var capA = vertices.Count;
        vertices.Add(a);
        var capB = vertices.Count;
        vertices.Add(b);

        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            triangles.Add((capA, ringAStart + next, ringAStart + i));
            triangles.Add((capB, ringBStart + i, ringBStart + next));
        }

        return true;
    }

    private static double GetBoundsDiagonal(IReadOnlyList<Point3D> vertices)
    {
        if (vertices.Count == 0)
        {
            return 1.0;
        }

        var minX = vertices[0].X;
        var minY = vertices[0].Y;
        var minZ = vertices[0].Z;
        var maxX = minX;
        var maxY = minY;
        var maxZ = minZ;

        for (var i = 1; i < vertices.Count; i++)
        {
            var p = vertices[i];
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Z < minZ) minZ = p.Z;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Z > maxZ) maxZ = p.Z;
        }

        var dx = maxX - minX;
        var dy = maxY - minY;
        var dz = maxZ - minZ;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
