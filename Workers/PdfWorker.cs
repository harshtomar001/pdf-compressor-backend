namespace pdf_compressor.Workers;
using Microsoft.AspNetCore.SignalR;
using pdf_compressor.Helper;
using pdf_compressor.Hubs;
using Microsoft.Extensions.Hosting;
using pdf_compressor.Models;
using pdf_compressor.Service;

public class PdfWorker : BackgroundService
{
    private readonly PdfQueueService _queue;
    private readonly GhostscriptService _ghostscriptService;
    private readonly MuPdfService _muPdfService;
    private readonly QPdfService _qPdfService;
    private readonly IHubContext<PdfHub> _hub;
    
    
    private static readonly SemaphoreSlim workerLimit = new SemaphoreSlim(3);

    public PdfWorker(
        PdfQueueService queue,
        GhostscriptService ghostscriptService,
        MuPdfService muPdfService,
        QPdfService qPdfService,
        IHubContext<PdfHub> hub)
    {
        _queue = queue;
        _ghostscriptService = ghostscriptService;
        _muPdfService = muPdfService;
        _qPdfService = qPdfService;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        Console.WriteLine("PDF Worker Started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out PdfJob? job)) // check if  Queue is still left over 
            {
                await workerLimit.WaitAsync(stoppingToken);


                try
                {
                    job.Status = "Processing";
                    await JobStorage.SaveJobAsync(job);
                    
                    Console.WriteLine($"Processing Job: {job.JobId}");
                    
                    await _hub.Clients.Group(job.JobId)
                        .SendAsync("JobUpdate", "Compression started");

                    string[] messages =
                    {
                        "Processing .",
                        "Processing . .",
                        "Processing . . ."
                    };

                    int i = 0;

                    var compressionTask = _ghostscriptService.CompressPdfAsync(
                        job.InputPath,
                        job.OutputPath,
                        "ebook"
                    );
                    
                    
                    await Task.Delay(800, stoppingToken);

                    while (!compressionTask.IsCompleted)
                    {
                        await _hub.Clients.Group(job.JobId)
                            .SendAsync("JobUpdate", messages[i % messages.Length]);

                        i++;

                        await Task.Delay(1000, stoppingToken);
                    }

                    await compressionTask;
                    
                    
                    
                    // await _qPdfService.OptimizePdfAsync(job.InputPath, job.OutputPath);


                    job.Status = "Completed";
                    
                    await JobStorage.SaveJobAsync(job);
                    
                    _queue.RemoveJob(job);

                    NotifyQueuePositions();
                    
                    job.Completion.SetResult(true);
                    
                    await _hub.Clients.Group(job.JobId)
                        .SendAsync("JobUpdate", "Compression Completed");
                    
                    long sizeInBytes = new FileInfo(job.InputPath).Length;
                    long InBytes = new FileInfo(job.OutputPath).Length;

                    Console.WriteLine(sizeInBytes + "   " + InBytes);

                    Console.WriteLine($"Completed Job: {job.JobId}");
                }
                catch (Exception ex)
                {

                    job.Status = "Failed";
                    Console.WriteLine("Worker error:");
                    
                    await _hub.Clients.Group(job.JobId)
                        .SendAsync("JobUpdate", "Compression Failed");
                    
                    Console.WriteLine(ex.ToString());
                    await JobStorage.SaveJobAsync(job);
                    job.Completion.SetResult(false);

                    Console.WriteLine($"Failed Job: {job.JobId}");

                    // Console.WriteLine(ex.Message);
                }
                finally
                {
                    workerLimit.Release();
                }
            }
            else
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
    
    private async Task NotifyQueuePositions()
    {
        var positions = _queue.GetWaitingPositions();

        foreach (var item in positions)
        {
            await _hub.Clients
                .Group(item.Job.JobId)
                .SendAsync(
                    "QueuePosition",
                    item.Position
                );
        }
    }
}