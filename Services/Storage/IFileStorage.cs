namespace pdf_compressor.Services.Storage;

using pdf_compressor.Models;

public interface IFileStorage
{
    Task<(string inputPath, string outputPath, string jobId)>
        CreateJobFilesAsync(IFormFile file);

    Task SaveJobAsync(PdfJob job);

    Task<string?> ReadJobAsync(string jobId);

    Task DeleteJobAsync(string jobId);
    
    Task<IReadOnlyList<PdfJob>> GetStoredJobsAsync();
    
    Task<IReadOnlyList<string>> GetOrphanedJobFoldersAsync(
        DateTime cutoff);
    
    Task DeleteOrphanedFolderAsync(string folderPath);
    
    
    
}