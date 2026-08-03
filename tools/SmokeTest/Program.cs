using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using ViewMd;

var mdPath = args.Length > 0 ? args[0] : "/tmp/sample.md";
var outPath = args.Length > 1 ? args[1] : "/tmp/smoketest.png";

var builder = AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

builder.SetupWithoutStarting();

var window = new MainWindow(mdPath);
window.Show();

// Let layout/render passes settle.
for (var i = 0; i < 5; i++)
{
    Dispatcher.UIThread.RunJobs();
}

var frame = window.CaptureRenderedFrame();
if (frame is null)
{
    Console.Error.WriteLine("CaptureRenderedFrame returned null");
    Environment.Exit(1);
}

frame.Save(outPath);
Console.WriteLine($"Saved {outPath}");

if (args.Length > 2)
{
    // Exercise MarkdownRenderer + PdfExportService directly, end to end, in their
    // own throwaway window (avoids reaching into MainWindow's private fields).
    var pdfPath = args[2];
    var renderer = new ViewMd.Rendering.MarkdownRenderer(Path.GetDirectoryName(mdPath)!, _ => { }, ViewMd.Rendering.MarkdownRenderOptions.Default);
    var content = renderer.Render(File.ReadAllText(mdPath));

    var pdfWindow = new Avalonia.Controls.Window { Content = content, Width = 1000, Height = 1500 };
    pdfWindow.Show();
    for (var i = 0; i < 5; i++)
    {
        Dispatcher.UIThread.RunJobs();
    }

    ViewMd.Services.PdfExportService.Export(content, pdfPath);
    Console.WriteLine($"Saved {pdfPath}");
}
