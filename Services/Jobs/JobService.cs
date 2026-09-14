using System.Collections.Concurrent;
using System.Text.Json;
using pdf_compressor.Models;
using pdf_compressor.Services.Storage;

namespace pdf_compressor.Services.Jobs;

public class JobService : IJobService
{
    private readonly ConcurrentDictionary<string, PdfJob> _jobs = new();
    private readonly IFileStorage _fileStorage;

    public JobService(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    public PdfJob CreateJob(
        string jobId,
        string inputPath,
        string outputPath,
        string compressionEngine,
        CompressionOptions compression)
    {
        return new PdfJob
        {
            JobId = jobId,
            InputPath = inputPath,
            OutputPath = outputPath,
            Status = JobStatus.Queued,
            CompressionEngine = compressionEngine,
            Compression = compression
        };
    }

    public void AddJob(PdfJob job)
    {
        _jobs[job.JobId] = job;
    }

    public PdfJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);

        return job;
    }

    public async Task<PdfJob?> GetJobAsync(string jobId)
    {
        // First check the in-memory job collection.
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return job;
        }

        // If it isn't in memory, try persistent storage.
        string? json = await _fileStorage.ReadJobAsync(jobId);

        if (json == null)
        {
            return null;
        }

        job = JsonSerializer.Deserialize<PdfJob>(json);

        if (job == null)
        {
            return null;
        }

        // Restore it into memory for future requests.
        _jobs[job.JobId] = job;

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