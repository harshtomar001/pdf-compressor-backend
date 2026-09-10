using System.Collections.Concurrent;
using pdf_compressor.Models;
namespace pdf_compressor.Services.Jobs;

public class JobService : IJobService
{
    private readonly ConcurrentDictionary<string, PdfJob> _jobs = new();

    public void AddJob(PdfJob job)
    {
        _jobs[job.JobId] = job;
    }

    public PdfJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);

        return job;
    }

    public void UpdateJob(PdfJob job)
    {
        _jobs[job.JobId] = job;
    }

    public void RemoveJob(string jobId)
    {
        _jobs.TryRemove(jobId, out _);
    }

    public IReadOnlyCollection<PdfJob> GetAllJobs()
    {
        return _jobs.Values.ToList().AsReadOnly();
    }
}