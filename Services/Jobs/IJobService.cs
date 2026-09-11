using pdf_compressor.Models;

namespace pdf_compressor.Services.Jobs;

public interface IJobService
{
    PdfJob CreateJob(
        string jobId,
        string inputPath,
        string outputPath);

    void AddJob(PdfJob job);

    PdfJob? GetJob(string jobId);

    void UpdateJob(PdfJob job);

    void RemoveJob(string jobId);

    IReadOnlyCollection<PdfJob> GetAllJobs();
}