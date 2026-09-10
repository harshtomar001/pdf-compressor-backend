using System.Text.Json;
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
    
    private readonly SemaphoreSlim workerLimit = new(3);

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
        this._fileStorage = fileStorage;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out PdfJob? job))
            {
                await workerLimit.WaitAsync(stoppingToken);

                try
                {
                    job.Status = JobStatus.Processing;
                    
                    _jobService.UpdateJob(job);

                    await _fileStorage.SaveJobAsync(
                        job.JobId,
                        JsonSerializer.Serialize(
                            job,
                            new JsonSerializerOptions
                            {
                                WriteIndented = true
                            }
                        )
                    );

                    Console.WriteLine($"Processing Job: {job.JobId}");

                    // Notify client that compression has started
                    await _hub.Clients
                        .Group(job.JobId)
                        .SendAsync(
                            "JobUpdate",
                            "Compression started",
                            stoppingToken);

                    // Get compression engine from router
                    var engine = _compressionRouter.GetEngine("ghostscript");

                    // Perform compression
                    await engine.CompressAsync(
                        job.InputPath,
                        job.OutputPath,
                        new CompressionOptions
                        {
                            Profile = "balanced"
                        },
                        stoppingToken);

                    // Compression completed successfully
                    job.Status = JobStatus.Completed;
                    _jobService.UpdateJob(job);

                    await _fileStorage.SaveJobAsync(
                        job.JobId,
                        JsonSerializer.Serialize(
                            job,
                            new JsonSerializerOptions
                            {
                                WriteIndented = true
                            }
                        )
                    );

                    await NotifyQueuePositions();

                    // Complete waiting tasks
                    job.Completion.SetResult(true);

                    // Notify client
                    await _hub.Clients
                        .Group(job.JobId)
                        .SendAsync(
                            "JobUpdate",
                            "Compression Completed",
                            stoppingToken);

                    // Log file sizes
                    long inputSize = new FileInfo(job.InputPath).Length;
                    long outputSize = new FileInfo(job.OutputPath).Length;

                    Console.WriteLine($"Input size: {inputSize} bytes");
                    Console.WriteLine($"Output size: {outputSize} bytes");
                    Console.WriteLine($"Completed Job: {job.JobId}");
                    
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    
                    Console.WriteLine($"Worker stopping while processing Job: {job.JobId}");

                    break;
                }
                catch (Exception ex)
                {
                    job.Status = JobStatus.Failed;
                    _jobService.UpdateJob(job);

                    Console.WriteLine($"Worker error for Job: {job.JobId}");

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

                    await _fileStorage.SaveJobAsync(
                        job.JobId,
                        JsonSerializer.Serialize(
                            job,
                            new JsonSerializerOptions
                            {
                                WriteIndented = true
                            }
                        )
                    );

                    job.Completion.SetResult(false);

                    Console.WriteLine($"Failed Job: {job.JobId}");
                }
                finally
                {
                    workerLimit.Release();
                }
            }
            else
            {
                await Task.Delay(
                    1000,
                    stoppingToken);
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
}
    
   
