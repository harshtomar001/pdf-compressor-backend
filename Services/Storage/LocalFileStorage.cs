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

    public async Task SaveJobAsync(string jobId, string json)
    {
        string jobFolder = Path.Combine(_basePath, jobId);

        Directory.CreateDirectory(jobFolder);

        string jobFile = Path.Combine(jobFolder, "job.json");

        await File.WriteAllTextAsync(jobFile, json);
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