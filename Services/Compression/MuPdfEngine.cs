using System.Diagnostics;
using pdf_compressor.Models;
using Microsoft.Extensions.Options;
using pdf_compressor.Configuration;

namespace pdf_compressor.Services.Compression;

public class MuPdfEngine : IPdfCompressionEngine
{
    private readonly string _mupdfPath;

    public MuPdfEngine(IOptions<PdfToolOptions> options)
    {
        _mupdfPath = options.Value.MuPdf;
        
        if (string.IsNullOrWhiteSpace(_mupdfPath))
        {
            throw new Exception("Ghostscript path not configured");
        }
        
    }

    public async Task CompressAsync(
        string inputPath,
        string outputPath,
        CompressionOptions options,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _mupdfPath,
            Arguments = $"clean -gggg \"{inputPath}\" \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception($"MuPDF failed: {error}");
        }
    }
}