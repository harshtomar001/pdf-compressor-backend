using System.Diagnostics;
using pdf_compressor.Models;
using Microsoft.Extensions.Options;
using pdf_compressor.Configuration;

namespace pdf_compressor.Services.Compression;

public class QPdfEngine : IPdfCompressionEngine
{
    private readonly string _qpdfPath;

    public QPdfEngine(IOptions<PdfToolOptions> options)
    {
        _qpdfPath = options.Value.QPdf;
        
        if (string.IsNullOrWhiteSpace(_qpdfPath))
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
            FileName = _qpdfPath,
            Arguments = $"--linearize \"{inputPath}\" \"{outputPath}\"",
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

        string output =
            await process.StandardOutput.ReadToEndAsync(cancellationToken);

        string error =
            await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception($"QPDF failed: {error}");
        }
    }
}