namespace pdf_compressor.Services.Storage;

public interface IFileStorage
{
    Task SaveJobAsync(string jobId, string json);

    Task<string?> ReadJobAsync(string jobId);

    Task DeleteJobAsync(string jobId);
}