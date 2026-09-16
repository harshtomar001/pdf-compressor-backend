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

        var startInfo = new ProcessStartInfo
        {
            FileName = _gsPath,

            Arguments =
                $"-sDEVICE=pdfwrite " +
                $"-dCompatibilityLevel=1.4 " +
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

        var stopwatch = Stopwatch.StartNew();

        process.Start();
        string error = null;

        try
        {
            string output =
                await process.StandardOutput.ReadToEndAsync(
                    cancellationToken);

               error =
                await process.StandardError.ReadToEndAsync(
                    cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();

            throw;
        }

        stopwatch.Stop();

        Console.WriteLine(
            $"Ghostscript process time: " +
            $"{stopwatch.Elapsed.TotalMilliseconds:F3} ms");

        if (process.ExitCode != 0)
        {
            throw new Exception(
                $"Ghostscript failed: {error}");
        }
    }
}