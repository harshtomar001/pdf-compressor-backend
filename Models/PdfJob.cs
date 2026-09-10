using System.Text.Json.Serialization;

namespace pdf_compressor.Models;

public class PdfJob
{
    public string JobId { get; set; } = "";
    public string InputPath { get; set; } = "";
    public string OutputPath { get; set; } = "";

    public JobStatus Status { get; set; } = JobStatus.Queued;

    [JsonIgnore]
    public TaskCompletionSource<bool> Completion { get; set; } = new();
}