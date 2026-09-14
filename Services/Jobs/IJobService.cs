using pdf_compressor.Models;

namespace pdf_compressor.Services.Jobs;

public interface IJobService
{
    PdfJob CreateJob(
        string jobId,
        string inputPath,
        string outputPath,
        string compressionEngine,
        CompressionOptions compression);

    void AddJob(PdfJob job);

    PdfJob? GetJob(string jobId);

    Task<PdfJob?> GetJobAsync(string jobId);

    void UpdateJob(PdfJob job);

    void RemoveJob(string jobId);

    IReadOnlyCollection<PdfJob> GetAllJobs();
    
    Task RemoveJobAsync(string jobId);
    
}