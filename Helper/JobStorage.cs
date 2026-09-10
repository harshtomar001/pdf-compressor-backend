using System.Text.Json;
using pdf_compressor.Models;

namespace pdf_compressor.Helper;

// update the json 
public static class JobStorage
{
    public static async Task SaveJobAsync(PdfJob job)
    {
        string jobFolder =
            Path.Combine("PDF_folder", job.JobId);

        Directory.CreateDirectory(jobFolder);

        string jobFile =
            Path.Combine(jobFolder, "job.json");

        string json =
            JsonSerializer.Serialize(
                job,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        await File.WriteAllTextAsync(
            jobFile,
            json
        );
    }

}
