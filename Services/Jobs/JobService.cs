using System.Collections.Concurrent;
using System.Text.Json;
using pdf_compressor.Models;
using pdf_compressor.Services.Storage;
using System.Security.Cryptography;
using System.Text;

namespace pdf_compressor.Services.Jobs;

public class JobService : IJobService
{
    private readonly ConcurrentDictionary<string, PdfJob> _jobs = new();
    private readonly IFileStorage _fileStorage;

    public JobService(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    public (PdfJob job, string accessToken) CreateJob(
        string jobId,
        string inputPath,
        string outputPath,
        string compressionEngine,
        CompressionOptions compression)
    {
        
        if (compression == null)
        {
            throw new ArgumentNullException(
                nameof(compression));
        }

        if (compression.Profile != "low" &&
            compression.Profile != "balanced" &&
            compression.Profile != "high")
        {
            throw new ArgumentException(
                "Invalid compression profile.",
                nameof(compression));
        }

        if (compressionEngine != "ghostscript" &&
            compressionEngine != "mupdf" &&
            compressionEngine != "qpdf")
        {
            throw new ArgumentException(
                "Invalid compression engine.",
                nameof(compressionEngine));
        }
        
        string accessToken = GenerateAccessToken();

        using var sha256 = SHA256.Create();

        byte[] hashBytes = sha256.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes(accessToken)
        );

        string accessTokenHash =
            Convert.ToHexString(hashBytes);

        var job = new PdfJob
        {
            JobId = jobId,
            AccessTokenHash = accessTokenHash,
            InputPath = inputPath,
            OutputPath = outputPath,
            Status = JobStatus.Queued,
            CompressionEngine = compressionEngine,
            Compression = compression
        };

        return (job, accessToken);
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
        if (!Guid.TryParse(jobId, out _))
        {
            return null;
        }

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


    public async Task RemoveJobAsync(string jobId)
    {
        _jobs.TryRemove(jobId, out _);

        await _fileStorage.DeleteJobAsync(jobId);
    }

    public IReadOnlyCollection<PdfJob> GetAllJobs()
    {
        return _jobs.Values.ToList().AsReadOnly();
    }

    public string GenerateAccessToken()
    {
        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
    
    public bool ValidateAccessToken(
        PdfJob job,
        string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        using var sha256 = SHA256.Create();

        byte[] hashBytes = sha256.ComputeHash(
            Encoding.UTF8.GetBytes(accessToken)
        );

        string accessTokenHash =
            Convert.ToHexString(hashBytes);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(job.AccessTokenHash),
            Convert.FromHexString(accessTokenHash)
        );
    }
    
}