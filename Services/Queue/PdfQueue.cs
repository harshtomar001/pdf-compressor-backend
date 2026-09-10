using System.Collections.Concurrent;
using pdf_compressor.Models;

namespace pdf_compressor.Services.Queue;

public class PdfQueue : IPdfQueue
{
    private readonly ConcurrentQueue<PdfJob> _queue = new();

    public void Enqueue(PdfJob job)
    {
        _queue.Enqueue(job);
    }

    public bool TryDequeue(out PdfJob? job)
    {
        return _queue.TryDequeue(out job);
    }

    public IReadOnlyList<PdfJob> GetQueuedJobs()
    {
        return _queue.ToArray();
    }
    
    public int GetPosition(string jobId)
    {
        var jobs = _queue.ToArray();

        for (int i = 0; i < jobs.Length; i++)
        {
            if (jobs[i].JobId == jobId)
            {
                return i + 1;
            }
        }

        return 0;
    }
}