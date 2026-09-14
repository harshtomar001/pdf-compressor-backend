using pdf_compressor.Configuration;
using pdf_compressor.Models;
using Microsoft.Extensions.Options;


namespace pdf_compressor.Services.Jobs;


public class JobCleanupService
{
    private readonly IJobService _jobService;
    private readonly JobCleanupOptions _options;

    public JobCleanupService(
        IJobService jobService,
        IOptions<JobCleanupOptions> options)
    {
        _jobService = jobService;
        _options = options.Value;
    }

    public async Task CleanupAsync()
    {
        var jobs = _jobService.GetAllJobs();

        DateTime cutoff = DateTime.UtcNow.AddMinutes(
            -_options.RetentionMinutes
        );

        foreach (PdfJob job in jobs)
        {
            if (job.CreatedAt >= cutoff)
            {
                continue;
            }

            try
            {
                await _jobService.RemoveJobAsync(job.JobId);

                Console.WriteLine(
                    $"Cleaned up expired job: {job.JobId}"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Failed to clean up job {job.JobId}: {ex}"
                );
            }
        }
    }
}