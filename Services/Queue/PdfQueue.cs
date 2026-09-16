using System.Collections.Concurrent;
using pdf_compressor.Models;

namespace pdf_compressor.Services.Queue;

public class PdfQueue : IPdfQueue, IDisposable
{
    private readonly SemaphoreSlim _signal = new(0);
    private readonly ConcurrentQueue<PdfJob> _queue = new();

    public void Enqueue(PdfJob job)
    {
        _queue.Enqueue(job);

        Console.WriteLine(
            $"QUEUE ENQUEUE: {job.JobId}"
        );

        _signal.Release();
    }

    public bool TryDequeue(out PdfJob? job)
    {
        var result = _queue.TryDequeue(out job);

        if (result && job != null)
        {
            Console.WriteLine(
                $"QUEUE DEQUEUE: {job.JobId}"
            );
        }

        return result;
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
    
    public async Task WaitForJobAsync(
        CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken); // Wait until the semaphore has a permit available
    }
    
    public void Dispose() // ASP.NET Core's DI container will dispose the singleton when the application shuts down.
    {
        _signal.Dispose();
    }
}