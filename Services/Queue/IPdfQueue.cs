using pdf_compressor.Models;

namespace pdf_compressor.Services.Queue;

public interface IPdfQueue
{
    void Enqueue(PdfJob job);

    bool TryDequeue(out PdfJob? job);

    IReadOnlyList<PdfJob> GetQueuedJobs();
    
    int GetPosition(string jobId);
}