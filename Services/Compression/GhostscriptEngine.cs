using System.Diagnostics;
using pdf_compressor.Models;
using Microsoft.Extensions.Options;
using pdf_compressor.Configuration;

namespace pdf_compressor.Services.Compression;

public class GhostscriptEngine : IPdfCompressionEngine
{
    private readonly string _gsPath;

    public GhostscriptEngine(IOptions<PdfToolOptions> options)
    {
        _gsPath = options.Value.Ghostscript;

        if (string.IsNullOrWhiteSpace(_gsPath))
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
        var compressionLevel = options.Profile switch
        {
            "low" => "screen",
            "balanced" => "ebook",
            "high" => "printer",
            _ => "ebook"
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = _gsPath,
            Arguments =
                $"-sDEVICE=pdfwrite " +
                $"-dCompatibilityLevel=1.4 " +
                $"-dPDFSETTINGS=/{compressionLevel} " +
                $"-dNOPAUSE " +
                $"-dQUIET " +
                $"-dBATCH " +
                $"-sOutputFile=\"{outputPath}\" " +
                $"\"{inputPath}\"",

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
            throw new Exception($"Ghostscript failed: {error}");
        }
    }
}