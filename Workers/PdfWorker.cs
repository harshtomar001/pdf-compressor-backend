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
    
    

    public PdfWorker(
        IPdfQueue queue,
        IJobService jobService,
        CompressionRouter compressionRouter,
        IHubContext<PdfHub> hub,
        IFileStorage fileStorage
    )
    {
        _queue = queue;
        _jobService = jobService;
        _compressionRouter = compressionRouter;
        _hub = hub;
        _fileStorage = fileStorage;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var workers = new[]
        {
            ProcessQueueAsync(stoppingToken),
            ProcessQueueAsync(stoppingToken)
        }; //  only 2 task are allowed at a time 

        await Task.WhenAll(workers); //  Wait until ALL tasks inside workers have finished.
    }
    
    private async Task ProcessQueueAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _queue.WaitForJobAsync(stoppingToken); //  waits until the queue is empty 

            if (_queue.TryDequeue(out PdfJob? job) && job != null)
            {
                await ProcessJobAsync(job, stoppingToken);
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
    CancellationToken stoppingToken)
    {
        try
        {
            job.Status = JobStatus.Processing;

            _jobService.UpdateJob(job);

            await _fileStorage.SaveJobAsync(job);

            Console.WriteLine($"Engine: {job.CompressionEngine}");
            Console.WriteLine($"Profile: {job.Compression.Profile}");
            Console.WriteLine($"Processing Job: {job.JobId}");

            await _hub.Clients
                .Group(job.JobId)
                .SendAsync(
                    "JobUpdate",
                    "Compression started",
                    stoppingToken);

            var engine = _compressionRouter.GetEngine(
                job.CompressionEngine);

            await engine.CompressAsync(
                job.InputPath,
                job.OutputPath,
                job.Compression,
                stoppingToken);

            job.Status = JobStatus.Completed;

            _jobService.UpdateJob(job);

            await _fileStorage.SaveJobAsync(job);

            await NotifyQueuePositions();

            job.Completion.SetResult(true);

            await _hub.Clients
                .Group(job.JobId)
                .SendAsync(
                    "JobUpdate",
                    "Compression Completed",
                    stoppingToken);

            long inputSize =
                new FileInfo(job.InputPath).Length;

            long outputSize =
                new FileInfo(job.OutputPath).Length;

            Console.WriteLine(
                $"Input size: {inputSize} bytes");

            Console.WriteLine(
                $"Output size: {outputSize} bytes");

            Console.WriteLine(
                $"Completed Job: {job.JobId}");
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine(
                $"Worker stopping while processing Job: {job.JobId}");

            return;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;

            _jobService.UpdateJob(job);

            Console.WriteLine(
                $"Worker error for Job: {job.JobId}");

            Console.WriteLine(ex.ToString());

            try
            {
                await _hub.Clients
                    .Group(job.JobId)
                    .SendAsync(
                        "JobUpdate",
                        "Compression Failed");
            }
            catch
            {
                // Ignore SignalR failure while handling
                // the original compression error.
            }

            await _fileStorage.SaveJobAsync(job);

            job.Completion.SetResult(false);

            Console.WriteLine(
                $"Failed Job: {job.JobId}");
        }
    }
        
    

}