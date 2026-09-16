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
        
        // we assign a group to each jobID to send the message to the user
        // with that job Id  (so that chat will  server to one not to all )
    }
}