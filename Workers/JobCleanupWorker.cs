using pdf_compressor.Services.Jobs;

namespace pdf_compressor.Workers;

public class JobCleanupWorker : BackgroundService
{
    private readonly JobCleanupService _cleanupService;

    public JobCleanupWorker(JobCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _cleanupService.CleanupAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Job cleanup worker error: {ex}"
                );
            }

            await Task.Delay(
                _cleanupService.CleanupInterval,
                stoppingToken
            );
        }
    }
}