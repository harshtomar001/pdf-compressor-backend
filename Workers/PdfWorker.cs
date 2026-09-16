using System.Diagnostics;
using pdf_compressor.Hubs;
using pdf_compressor.Models;
using pdf_compressor.Services.Compression;
using Microsoft.AspNetCore.SignalR;
using pdf_compressor.Services.Jobs;
using pdf_compressor.Services.Queue;
using pdf_compressor.Services.Storage;


namespace pdf_compressor.Workers;

public class PdfWorker : BackgroundService
{
    private readonly IPdfQueue _queue;
    private readonly IJobService _jobService;
    private readonly CompressionRouter _compressionRouter;
    private readonly IHubContext<PdfHub> _hub;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<PdfWorker> _logger;
    
    private readonly IJobCancellationService _jobCancellationService;

    public PdfWorker(
        IPdfQueue queue,
        IJobService jobService,
        CompressionRouter compressionRouter,
        IHubContext<PdfHub> hub,
        IFileStorage fileStorage,
        IJobCancellationService jobCancellationService,
        ILogger<PdfWorker> logger
    )
    {
        _queue = queue;
        _jobService = jobService;
        _compressionRouter = compressionRouter;
        _hub = hub;
        _fileStorage = fileStorage;
        _jobCancellationService = jobCancellationService;
        _logger = logger;
    }
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Recover jobs that were not in a terminal state
        // when the application previously stopped.
        var storedJobs = await _fileStorage.GetStoredJobsAsync();

        foreach (var job in storedJobs)
        {
            
            if (!File.Exists(job.InputPath))
            {
                Console.WriteLine(
                    $"Recovered job has missing input file: {job.JobId}"
                );

                job.Status = JobStatus.Failed;
                job.ErrorMessage = "Input PDF is missing.";

                _jobService.UpdateJob(job);

                await _fileStorage.SaveJobAsync(job);

                job.Completion.TrySetResult(false);

                continue;
            }
            
            
            if (job.Status == JobStatus.Queued ||
                job.Status == JobStatus.Processing)
            {
                Console.WriteLine(
                    $"Recovering job: {job.JobId} " +
                    $"(previous status: {job.Status})");

                job.Status = JobStatus.Queued;
                job.ErrorMessage = null;

                _jobService.UpdateJob(job);

                await _fileStorage.SaveJobAsync(job);

                _queue.Enqueue(job);
            }
        }

        var workers = new[]
        {
            ProcessQueueAsync(stoppingToken),
            ProcessQueueAsync(stoppingToken)
        };//  only 2 task are allowed at a time 

        await Task.WhenAll(workers); //  Wait until ALL tasks inside workers have finished.
    }
    
    
    
    private async Task ProcessQueueAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _queue.WaitForJobAsync(stoppingToken); //  waits until the queue is empty 

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (_queue.TryDequeue(out PdfJob? job) && job != null)
            {
                var jobToken = _jobCancellationService.Register(job.JobId); // register for ( if user cancel the compression then it can stop the process )

                try
                {
                    await ProcessJobAsync(job, jobToken);
                }
                finally
                {
                    _jobCancellationService.Remove(job.JobId);
                } 
            }
        }
    }

    private async Task NotifyQueuePositions()
    {
        var queuedJobs = _queue.GetQueuedJobs();

        for (int i = 0; i < queuedJobs.Count; i++)
        {
            await _hub.Clients
                .Group(queuedJobs[i].JobId)
                .SendAsync(
                    "QueuePosition",
                    i + 1
                );
        }
    }
    

     private async Task ProcessJobAsync(
        PdfJob job,
        CancellationToken jobCancellationToken)
    {
        try
        {
            job.Status = JobStatus.Processing;
            job.ErrorMessage = null;

            _jobService.UpdateJob(job);

            await _fileStorage.SaveJobAsync(job);

            _logger.LogInformation(
                "Starting PDF compression. JobId: {JobId}, Engine: {Engine}, Profile: {Profile}",
                job.JobId,
                job.CompressionEngine,
                job.Compression.Profile
            );

            await _hub.Clients
                .Group(job.JobId)
                .SendAsync(
                    "JobUpdate",
                    "Compression started",
                    jobCancellationToken
                );

            var engine =
                _compressionRouter.GetEngine(
                    job.CompressionEngine);

            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Compression started. JobId: {JobId}",
                job.JobId
            );

            await engine.CompressAsync(
                job.InputPath,
                job.OutputPath,
                job.Compression,
                jobCancellationToken
            );

            stopwatch.Stop();

            _logger.LogInformation(
                "Compression completed. JobId: {JobId}, DurationMs: {DurationMs:F3}",
                job.JobId,
                stopwatch.Elapsed.TotalMilliseconds
            );

            job.Status = JobStatus.Completed;

            _jobService.UpdateJob(job);

            await _fileStorage.SaveJobAsync(job);

            await NotifyQueuePositions();

            job.Completion.TrySetResult(true);

            await _hub.Clients
                .Group(job.JobId)
                .SendAsync(
                    "JobUpdate",
                    "Compression Completed",
                    jobCancellationToken
                );

            long inputSize =
                new FileInfo(job.InputPath).Length;

            long outputSize =
                new FileInfo(job.OutputPath).Length;

            _logger.LogInformation(
                "Compression result. JobId: {JobId}, InputBytes: {InputBytes}, OutputBytes: {OutputBytes}",
                job.JobId,
                inputSize,
                outputSize
            );
        }

        catch (OperationCanceledException)
            when (jobCancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "PDF compression cancelled. JobId: {JobId}",
                job.JobId
            );

            try
            {
                for (int attempt = 1; attempt <= 5; attempt++)
                {
                    if (!File.Exists(job.OutputPath))
                    {
                        break;
                    }

                    try
                    {
                        File.Delete(job.OutputPath);

                        _logger.LogInformation(
                            "Deleted partial output. JobId: {JobId}",
                            job.JobId
                        );

                        break;
                    }
                    catch (IOException) when (attempt < 5)
                    {
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(
                    cleanupEx,
                    "Failed to delete partial output. JobId: {JobId}",
                    job.JobId
                );
            }

            job.Status = JobStatus.Failed;
            job.ErrorMessage = "Job was cancelled.";

            _jobService.UpdateJob(job);

            await _fileStorage.SaveJobAsync(job);

            job.Completion.TrySetResult(false);

            try
            {
                await _hub.Clients
                    .Group(job.JobId)
                    .SendAsync(
                        "JobUpdate",
                        "Compression Cancelled"
                    );
            }
            catch
            {
                // Ignore SignalR failure while handling cancellation.
            }

            return;
        }

        catch (Exception ex)
        {
            try
            {
                if (File.Exists(job.OutputPath))
                {
                    File.Delete(job.OutputPath);

                    _logger.LogInformation(
                        "Deleted partial output after failure. JobId: {JobId}",
                        job.JobId
                    );
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(
                    cleanupEx,
                    "Failed to delete partial output after failure. JobId: {JobId}",
                    job.JobId
                );
            }

            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;

            _jobService.UpdateJob(job);

            _logger.LogError(
                ex,
                "PDF compression failed. JobId: {JobId}",
                job.JobId
            );

            try
            {
                await _hub.Clients
                    .Group(job.JobId)
                    .SendAsync(
                        "JobUpdate",
                        "Compression Failed"
                    );
            }
            catch
            {
                // Ignore SignalR failure while handling
                // the original compression error.
            }

            await _fileStorage.SaveJobAsync(job);

            job.Completion.TrySetResult(false);

            _logger.LogError(
                "Job marked as failed. JobId: {JobId}",
                job.JobId
            );
        }
    }
     
}