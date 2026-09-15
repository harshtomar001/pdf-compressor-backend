namespace pdf_compressor.Services.Jobs;

public interface IJobCancellationService
{
    CancellationToken Register(string jobId);
    bool Cancel(string jobId);
    void Remove(string jobId);
}