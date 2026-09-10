using System.Diagnostics;
using pdf_compressor.Models;

namespace pdf_compressor.Services.Compression;

public class QPdfEngine : IPdfCompressionEngine
{
    private readonly string _qpdfPath;

    public QPdfEngine(IConfiguration configuration)
    {
        _qpdfPath = configuration["PdfTools:QPdf"]
                    ?? throw new Exception("QPDF path not configured");
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