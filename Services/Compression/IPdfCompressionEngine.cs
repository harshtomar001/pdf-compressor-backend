using pdf_compressor.Models;

namespace pdf_compressor.Services.Compression;




public interface IPdfCompressionEngine
{
    Task CompressAsync(
        string inputPath,
        string outputPath,
        CompressionOptions options,
        CancellationToken cancellationToken);
}