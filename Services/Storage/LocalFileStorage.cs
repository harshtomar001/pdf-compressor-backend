using System.Text.Json;
using Microsoft.AspNetCore.Http;
using pdf_compressor.Models;

namespace pdf_compressor.Services.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;

    public LocalFileStorage(IWebHostEnvironment environment)
    {
        _basePath = Path.Combine(
            environment.ContentRootPath,
            "PDF_folder"
        );
    }

    public async Task<(string inputPath, string outputPath, string jobId)>
        CreateJobFilesAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new Exception("Corrupted file");
        }

        string fileExtension = Path.GetExtension(file.FileName);

        if (!string.Equals(
                fileExtension,
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception("Only PDF files are allowed");
        }

        string jobId = Guid.NewGuid().ToString();

        string jobFolder = Path.Combine(
            _basePath,
            jobId
        );

        Directory.CreateDirectory(jobFolder);

        string inputPath = Path.Combine(
            jobFolder,
            "input.pdf"
        );

        string outputPath = Path.Combine(
            jobFolder,
            "output.pdf"
        );

        await using var stream = new FileStream(
            inputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        );

        await file.CopyToAsync(stream);

        return (
            inputPath,
            outputPath,
            jobId
        );
    }

    public async Task SaveJobAsync(PdfJob job)
    {
        string jobFolder = Path.Combine(
            _basePath,
            job.JobId
        );

        Directory.CreateDirectory(jobFolder);

        string jobFile = Path.Combine(
            jobFolder,
            "job.json"
        );

        string json = JsonSerializer.Serialize(
            job,
            new JsonSerializerOptions
            {
                WriteIndented = true
            }
        );

        await File.WriteAllTextAsync(
            jobFile,
            json
        );
    }

    public async Task<string?> ReadJobAsync(string jobId)
    {
        string jobFile = Path.Combine(
            _basePath,
            jobId,
            "job.json"
        );

        if (!File.Exists(jobFile))
        {
            return null;
        }

        return await File.ReadAllTextAsync(jobFile);
    }

    public Task DeleteJobAsync(string jobId)
    {
        string jobFolder = Path.Combine(
            _basePath,
            jobId
        );

        if (Directory.Exists(jobFolder))
        {
            Directory.Delete(jobFolder, true);
        }

        return Task.CompletedTask;
    }
}