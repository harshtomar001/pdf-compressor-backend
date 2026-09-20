namespace pdf_compressor.Services.Monitoring;

public class WorkerMetrics
{
    private int _activeJobs;
    private long _completedJobs;
    private long _failedJobs;
    private long _cancelledJobs;

    public int ActiveJobs =>
        Volatile.Read(ref _activeJobs);

    public long CompletedJobs =>
        Volatile.Read(ref _completedJobs);

    public long FailedJobs =>
        Volatile.Read(ref _failedJobs);

    public long CancelledJobs =>
        Volatile.Read(ref _cancelledJobs);

    public void JobStarted()
    {
        Interlocked.Increment(ref _activeJobs);
    }

    public void JobCompleted()
    {
        Interlocked.Decrement(ref _activeJobs);
        Interlocked.Increment(ref _completedJobs);
    }

    public void JobFailed()
    {
        Interlocked.Decrement(ref _activeJobs);
        Interlocked.Increment(ref _failedJobs);
    }

    public void JobCancelled()
    {
        Interlocked.Decrement(ref _activeJobs);
        Interlocked.Increment(ref _cancelledJobs);
    }
}