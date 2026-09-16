using System.ComponentModel.DataAnnotations;

namespace pdf_compressor.Configuration;

public class PdfToolOptions
{
    [Required]
    public string Ghostscript { get; set; } = "";

    [Required]
    public string MuPdf { get; set; } = "";

    [Required]
    public string QPdf { get; set; } = "";
}

//          appsettings.json
//                ↓
//          PdfToolOptions
//                ↓
//      Dependency Injection
//                ↓
//      ┌─────────┼─────────┐
//      ↓         ↓         ↓
// Ghostscript  MuPdf      QPdf

