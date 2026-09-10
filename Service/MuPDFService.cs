namespace pdf_compressor.Service;
using System.Diagnostics;


public class MuPdfService

{
    private readonly string _mupdfPath;

    public MuPdfService(IConfiguration configuration)
    {
        _mupdfPath =configuration["PdfTools:MuPdf"]?? throw new Exception("MuPdfPath not configured"); 
    }

    public async Task<string> CompressPdfAsync(string inputPath, string outputPath)
    
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

        using var process = new Process();
        process.StartInfo = startInfo;

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"MuPDF failed: {error}");
        }

        return outputPath;
    }
}
