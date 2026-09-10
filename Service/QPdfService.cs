namespace pdf_compressor.Service;

using System.Diagnostics;


public class QPdfService
{
    private readonly string _qpdfPath;

    public QPdfService(IConfiguration configuration)
    {
        _qpdfPath = configuration["PdfTools:QPdf"]
                    ?? throw new Exception("QPdf path not configured");
    }
    

    public async Task<string> OptimizePdfAsync(string inputPath, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName =_qpdfPath,
            Arguments = $"--linearize \"{inputPath}\" \"{outputPath}\"",
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
            throw new Exception($"QPDF failed: {error}");
        }

        return outputPath;
    }
}