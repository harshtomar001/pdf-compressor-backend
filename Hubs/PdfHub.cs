namespace pdf_compressor.Hubs;
using Microsoft.AspNetCore.SignalR;

public class PdfHub : Hub
{
    public async Task JoinJob(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
        // we assign a group to each jobID to send the message to the user
        // with that job Id  (so that chat will  server to one not to all )
    }
}
