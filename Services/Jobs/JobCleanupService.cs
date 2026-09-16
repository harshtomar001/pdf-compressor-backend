using pdf_compressor.Configuration;
using pdf_compressor.Models;
using Microsoft.Extensions.Options;
using pdf_compressor.Services.Storage;

namespace pdf_compressor.Services.Jobs;


public class JobCleanupService
{
    private readonly IJobService _jobService;
    private readonly IFileStorage _fileStorage;
    private readonly JobCleanupOptions _options;
    
    
    public TimeSpan CleanupInterval =>
        TimeSpan.FromMinutes(_options.IntervalMinutes);

    public JobCleanupService(
        IJobService jobService,
        IFileStorage fileStorage,
        IOptions<JobCleanupOptions> options)
    {
        _jobService = jobService;
        _fileStorage = fileStorage;
        _options = options.Value;
    }

    public async Task CleanupAsync()
    {
        var jobs = await _fileStorage.GetStoredJobsAsync();

        DateTime cutoff = DateTime.UtcNow.AddMinutes(
            -_options.RetentionMinutes
        );

        // Existing persisted-job cleanup
        foreach (PdfJob job in jobs)
        {
            if (job.CreatedAt >= cutoff)
            {
                continue;
            }

            if (job.Status != JobStatus.Completed &&
                job.Status != JobStatus.Failed)
            {
                continue;
            }

            try
            {
                PdfJob? currentJob =
                    await _jobService.GetJobAsync(job.JobId);

                if (currentJob == null)
                {
                    continue;
                }

                if (currentJob.CreatedAt >= cutoff)
                {
                    continue;
                }

                if (currentJob.Status != JobStatus.Completed &&
                    currentJob.Status != JobStatus.Failed)
                {
                    continue;
                }

                await _jobService.RemoveJobAsync(currentJob.JobId);

                Console.WriteLine(
                    $"Cleaned up expired job: {currentJob.JobId}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to clean up job {job.JobId}: {ex}"
                );
            }
        }

        // Orphaned-folder cleanup
        var orphanedFolders =
            await _fileStorage.GetOrphanedJobFoldersAsync(cutoff);

        foreach (string folder in orphanedFolders)
        {
            try
            {
                await _fileStorage.DeleteOrphanedFolderAsync(folder);

                Console.WriteLine(
                    $"Cleaned up orphaned job folder: {folder}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to clean up orphaned job folder {folder}: {ex}"
                );
            }
        }
    }
}