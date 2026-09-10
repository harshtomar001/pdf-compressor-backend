namespace pdf_compressor.Service;
using System.Diagnostics;

public class GhostscriptService
{
    private readonly string _gsPath;

    public GhostscriptService(IConfiguration configuration)
    {
        _gsPath = configuration["PdfTools:Ghostscript"] 
                  ?? throw new Exception("Ghostscript path not configured");
    }

    public async Task<string> CompressPdfAsync(
        string inputPath,
        string outputPath,
        string compressionLevel = "ebook")
    {
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

        using var process = new Process();
        process.StartInfo = startInfo;

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Ghostscript failed: {error}");
        }

        return outputPath;
    }
}