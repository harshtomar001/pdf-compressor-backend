using System.ComponentModel.DataAnnotations;

namespace pdf_compressor.Models;

public class CompressionRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    [RegularExpression(
        "^(ghostscript|mupdf|qpdf)$",
        ErrorMessage = "Invalid compression engine."
    )]
    public string Engine { get; set; } = "ghostscript";

    [Required]
    [RegularExpression(
        "^(low|balanced|high)$",
        ErrorMessage = "Invalid compression profile."
    )]
    public string Profile { get; set; } = "balanced";
}