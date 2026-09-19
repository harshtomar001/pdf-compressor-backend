namespace pdf_compressor.Hubs;

using Microsoft.AspNetCore.SignalR;
using pdf_compressor.Models;
using pdf_compressor.Services.Jobs;

public class PdfHub : Hub
{
    private readonly IJobService _jobService;

    public PdfHub(IJobService jobService)
    {
        _jobService = jobService;
    }

    public async Task JoinJob(
        string jobId,
        string accessToken)
    {
        PdfJob? job = await _jobService.GetJobAsync(jobId);

        if (job == null)
        {
            throw new HubException("Job not found.");
        }

        if (!_jobService.ValidateAccessToken(
                job,
                accessToken))
        {
            throw new HubException("Invalid access token.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            jobId);

        // Send the current job status to the newly connected client.
        // This prevents fast jobs from being missed before SignalR connects.
       
        string message = job.Status switch
        {
            
            JobStatus.Processing =>
                "Compression started",

            JobStatus.Completed =>
                "Compression Completed",

            JobStatus.Failed =>
                "Compression Failed",

            _ =>
                "Job status unknown"
        };

        await Clients.Caller.SendAsync(
            "JobUpdate",
            message
        );
    }
}