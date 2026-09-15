using System.Text.Json.Serialization;

namespace pdf_compressor.Models;

public class PdfJob
{
    public string JobId { get; set; } = "";
    public string InputPath { get; set; } = "";
    public string OutputPath { get; set; } = "";

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public string CompressionEngine { get; set; } = "ghostscript";

    public CompressionOptions Compression { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ErrorMessage { get; set; }

    [JsonIgnore]
    public TaskCompletionSource<bool> Completion { get; set; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}