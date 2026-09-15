using System.Collections.Concurrent;

namespace pdf_compressor.Services.Jobs;

//  if user cancel the pdf compression then we can call the gs to kill the process

public class JobCancellationService : IJobCancellationService
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokens = new();

    public CancellationToken Register(string jobId)
    {
        var cts = new CancellationTokenSource();

        if (!_tokens.TryAdd(jobId, cts))
        {
            cts.Dispose();

            throw new InvalidOperationException(
                $"Cancellation already registered for job: {jobId}");
        }

        return cts.Token;
    }

    public bool Cancel(string jobId)
    {
        if (_tokens.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }

        return false;
    }

    public void Remove(string jobId)
    {
        if (_tokens.TryRemove(jobId, out var cts))
        {
            cts.Dispose();
        }
    }
}