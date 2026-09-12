using Microsoft.AspNetCore.Http;

namespace pdf_compressor.Models;

public class CompressionRequest
{
    public IFormFile File { get; set; } = null!;

    public string Engine { get; set; } = "ghostscript";

    public string Profile { get; set; } = "balanced";
}