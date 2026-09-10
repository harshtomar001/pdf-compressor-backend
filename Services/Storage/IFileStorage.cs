namespace pdf_compressor.Services.Storage;


public interface IFileStorage
{
    Task<(string inputPath, string outputPath, string jobId)> CreateJobFilesAsync(IFormFile file);

    Task SaveJobAsync(string jobId, string json);

    Task<string?> ReadJobAsync(string jobId);

    Task DeleteJobAsync(string jobId);
}