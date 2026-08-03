using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace ViewMd.Services;

// Exports the currently rendered document to a single-page PDF via SkiaSharp's
// SKDocument PDF canvas. Reuses Avalonia's own SkiaSharp dependency instead of
// pulling in a separate PDF library — see .charter/capabilities/export-to-pdf.md.
public static class PdfExportService
{
    public static void Export(Control renderedContent, string outputPath)
    {
        var width = (int)Math.Ceiling(renderedContent.Bounds.Width);
        var height = (int)Math.Ceiling(renderedContent.Bounds.Height);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Document has no rendered content to export.");
        }

        using var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        rtb.Render(renderedContent);

        using var pngStream = new MemoryStream();
        rtb.Save(pngStream);
        pngStream.Position = 0;

        using var skBitmap = SKBitmap.Decode(pngStream);
        using var pdfStream = new SKFileWStream(outputPath);
        using var document = SKDocument.CreatePdf(pdfStream);
        using var canvas = document.BeginPage(skBitmap.Width, skBitmap.Height);
        canvas.DrawBitmap(skBitmap, 0, 0);
        document.EndPage();
        document.Close();
    }
}
