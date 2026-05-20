using System;
using System.IO;
using System.Windows;
using DCMViewer.Services;

namespace DCMViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0)
        {
            RunCliParseMode(e.Args[0]);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        // Test code disabled
        // DCMViewer.Tests.ParserTester.RunTest();
        // Shutdown();
    }

    private static void RunCliParseMode(string inputPath)
    {
        try
        {
            var filePath = Path.GetFullPath(inputPath);
            Console.WriteLine($"[CLI] Parsing: {filePath}");

            var parser = new DcmParser();
            var result = parser.ParseFile(filePath);

            Console.WriteLine("[CLI] Parse succeeded.");
            Console.WriteLine($"[CLI] Vertices: {result.VertexCount}");
            Console.WriteLine($"[CLI] Triangles: {result.TriangleCount}");
            Console.WriteLine($"[CLI] Encrypted: {result.IsEncrypted}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[CLI] Parse failed: {ex.GetType().FullName}");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex.StackTrace);
        }
    }
}
