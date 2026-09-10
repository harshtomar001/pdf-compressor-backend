using pdf_compressor.Models;

namespace pdf_compressor.Services.Jobs;

public interface IJobService
{
    void AddJob(PdfJob job);

    PdfJob? GetJob(string jobId);

    void UpdateJob(PdfJob job);

    void RemoveJob(string jobId);

    IReadOnlyCollection<PdfJob> GetAllJobs();
}