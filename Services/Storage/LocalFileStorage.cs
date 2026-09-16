using System.Text.Json;
using pdf_compressor.Exceptions;
using pdf_compressor.Models;

namespace pdf_compressor.Services.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;
    private const long MinimumFreeSpaceBytes = 1L * 1024 * 1024 * 1024;

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

    string? root = Path.GetPathRoot(_basePath);

    if (string.IsNullOrWhiteSpace(root))
    {
        throw new IOException(
            "Unable to determine storage drive."
        );
    }

    var drive = new DriveInfo(root);

    if (!drive.IsReady)
    {
        throw new IOException(
            "Storage drive is not available."
        );
    }

    long requiredSpace =
        file.Length + MinimumFreeSpaceBytes;

    if (drive.AvailableFreeSpace < requiredSpace)
    {
        throw new IOException(
            "Not enough disk space to store this PDF safely."
        );
    }

    string jobId = Guid.NewGuid().ToString();

    string jobFolder = Path.Combine(
        _basePath,
        jobId
    );

    string inputPath = Path.Combine(
        jobFolder,
        "input.pdf"
    );

    string outputPath = Path.Combine(
        jobFolder,
        "output.pdf"
    );

    try
    {
        Directory.CreateDirectory(jobFolder);

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
    catch
    {
        try
        {
            if (Directory.Exists(jobFolder))
            {
                Directory.Delete(jobFolder, true);
            }
        }
        catch (Exception cleanupException)
        {
            Console.WriteLine(
                $"Failed to clean up job folder after upload failure: " +
                $"{jobFolder}"
            );

            Console.WriteLine(cleanupException);
        }

        throw;
    }
} 
       
    public async Task SaveJobAsync(PdfJob job)
    {
        string jobFolder = GetJobFolder(job.JobId);

        Directory.CreateDirectory(jobFolder);

        string jobFile = Path.Combine(
            jobFolder,
            "job.json"
        );

        string tempJobFile = Path.Combine(
            jobFolder,
            "job.json.tmp"
        );

        string json = JsonSerializer.Serialize(
            job,
            new JsonSerializerOptions
            {
                WriteIndented = true
            }
        );

        try
        {
            await File.WriteAllTextAsync(
                tempJobFile,
                json
            );

            File.Move(
                tempJobFile,
                jobFile,
                true
            );
        }
        catch
        {
            try
            {
                if (File.Exists(tempJobFile))
                {
                    File.Delete(tempJobFile);
                }
            }
            catch (Exception cleanupException)
            {
                Console.WriteLine(
                    $"Failed to clean up temporary job file: " +
                    $"{tempJobFile}"
                );

                Console.WriteLine(cleanupException);
            }

            throw;
        }
    }

    public async Task<string?> ReadJobAsync(string jobId)
    {
        string jobFolder = GetJobFolder(jobId);

        string jobFile = Path.Combine(
            jobFolder,
            "job.json"
        );

        if (!File.Exists(jobFile))
        {
            return null;
        }

        return await File.ReadAllTextAsync(jobFile);
    }

    public async Task DeleteJobAsync(string jobId)
    {
        string jobFolder = GetJobFolder(jobId);

        if (!Directory.Exists(jobFolder))
        {
            return;
        }

        const int maxAttempts = 5;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(jobFolder, true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(100);
            }
        }

        // Final attempt.
        Directory.Delete(jobFolder, true);
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
    
    public Task<IReadOnlyList<string>> GetOrphanedJobFoldersAsync(
        DateTime cutoff)
    {
        var orphanedFolders = new List<string>();

        if (!Directory.Exists(_basePath))
        {
            return Task.FromResult<IReadOnlyList<string>>(
                orphanedFolders.AsReadOnly()
            );
        }

        foreach (string jobFolder in Directory.GetDirectories(_basePath))
        {
            string folderName = Path.GetFileName(jobFolder);

            if (!Guid.TryParse(folderName, out _))
            {
                continue;
            }

            string jobFile = Path.Combine(
                jobFolder,
                "job.json"
            );

            if (File.Exists(jobFile))
            {
                continue;
            }

            DateTime lastWriteTime = Directory
                .GetLastWriteTimeUtc(jobFolder);

            if (lastWriteTime < cutoff)
            {
                orphanedFolders.Add(jobFolder);
            }
            
        }

        return Task.FromResult<IReadOnlyList<string>>(
            orphanedFolders.AsReadOnly()
        );
    }
    
    public async Task DeleteOrphanedFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return;
        }

        const int maxAttempts = 5;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(folderPath, true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                await Task.Delay(100);
            }
        }

        Directory.Delete(folderPath, true);
    }
 
    private string GetJobFolder(string jobId)
    {
        if (!Guid.TryParse(jobId, out var parsedJobId))
        {
            throw new ArgumentException(
                "Invalid job ID.",
                nameof(jobId)
            );
        }

        string basePath = Path.GetFullPath(_basePath);

        string jobFolder = Path.GetFullPath(
            Path.Combine(
                basePath,
                parsedJobId.ToString()
            )
        );

        if (!jobFolder.StartsWith(
                basePath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Invalid job storage path."
            );
        }

        return jobFolder;
    }
    
   
}