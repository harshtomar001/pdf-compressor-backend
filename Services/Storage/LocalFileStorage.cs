using System.Text.Json;
using Microsoft.AspNetCore.Http;
using pdf_compressor.Exceptions;
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
            throw new InvalidFileException(
                "The uploaded file is empty."
            );
        }

        string fileExtension = Path.GetExtension(file.FileName);

        if (!string.Equals(
                fileExtension,
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidFileException(
                "Only PDF files are allowed."
            );
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
    
    public async Task<IReadOnlyList<PdfJob>> GetStoredJobsAsync()
    {
        var jobs = new List<PdfJob>();

        if (!Directory.Exists(_basePath))
        {
            return jobs.AsReadOnly();
        }

        foreach (string jobFolder in Directory.GetDirectories(_basePath))
        {
            string jobFile = Path.Combine(
                jobFolder,
                "job.json"
            );

            if (!File.Exists(jobFile))
            {
                continue;
            }

            try
            {
                string json = await File.ReadAllTextAsync(jobFile);

                PdfJob? job = JsonSerializer.Deserialize<PdfJob>(json);

                if (job != null)
                {
                    jobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to read stored job: {jobFile}"
                );

                Console.WriteLine(ex);
            }
        }

        return jobs.AsReadOnly();
    }
    
}