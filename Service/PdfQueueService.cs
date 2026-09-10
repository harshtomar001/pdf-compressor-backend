using pdf_compressor.Models;

namespace pdf_compressor.Service;

using System.Collections.Concurrent;


public class PdfQueueService
{
    private readonly ConcurrentQueue<PdfJob> _queue = new();
    
    public List<PdfJob> Jobs { get; } = new(); // to get the position of the req // updating the position of the req after compression 
    
    private readonly ConcurrentDictionary<string, PdfJob> _allJobs = new(); // to fetch the output path of the particular job id  for download API

    public void Enqueue(PdfJob job)
    {
        _queue.Enqueue(job);
        lock (Jobs)
        {
            Jobs.Add(job);
        }

        lock (job)
        {
            _allJobs[job.JobId] = job;
            
        }
    }

    public bool TryDequeue(out PdfJob job)
    {
        return _queue.TryDequeue(out job);
    }
    
    public void RemoveJob(PdfJob job)
    {
        lock (Jobs)
        {
            Jobs.Remove(job);
        }
    }
    
    public int GetPosition(string jobId)
    {
        lock (Jobs)
        {
            return Jobs.FindIndex(
                x => x.JobId == jobId
            ) + 1;
        }
    }
    public List<(PdfJob Job, int Position)> GetWaitingPositions()
    {
        lock (Jobs)
        {
            return Jobs
                .Where(j => j.Status == "Queued")
                .Select((job, index) => (job, index + 1))
                .ToList();
        }
    }

    public int Count => _queue.Count;
    
    // getting the model if the jobId found for the download API
    public PdfJob? GetJobDict(string jobId)
    {
        _allJobs.TryGetValue(jobId, out var job);
        return job;
    }

    // Remove the  pair if the server sends the file to the request
    public bool RemoveJobDict(string jobId)
    {
        
        return _allJobs.TryRemove(jobId, out _);
        
    }
}