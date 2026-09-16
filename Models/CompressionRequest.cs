using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace pdf_compressor.Models;

public class CompressionRequest
{
    [Required(ErrorMessage = "PDF file is required.")]
    public IFormFile File { get; set; } = null!;

    [Required(ErrorMessage = "Compression engine is required.")]
    [RegularExpression(
        "^(ghostscript|mupdf|qpdf)$",
        ErrorMessage = "Invalid compression engine."
    )]
    public string Engine { get; set; } = "ghostscript";

    [Required(ErrorMessage = "Compression profile is required.")]
    [RegularExpression(
        "^(low|balanced|high)$",
        ErrorMessage = "Invalid compression profile."
    )]
    public string Profile { get; set; } = "balanced";
}