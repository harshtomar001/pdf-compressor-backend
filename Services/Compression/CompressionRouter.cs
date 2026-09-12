

namespace pdf_compressor.Services.Compression;

public class CompressionRouter
{
    private readonly GhostscriptEngine _ghostscript;
    private readonly MuPdfEngine _mupdf;
    private readonly QPdfEngine _qpdf;

    public CompressionRouter(
        GhostscriptEngine ghostscript,
        MuPdfEngine mupdf,
        QPdfEngine qpdf)
    {
        _ghostscript = ghostscript;
        _mupdf = mupdf;
        _qpdf = qpdf;
    }

    public IPdfCompressionEngine GetEngine(string engine)
    {
        return engine.ToLowerInvariant() switch
        {
            "ghostscript" => _ghostscript,
            "mupdf" => _mupdf,
            "qpdf" => _qpdf,

            _ => throw new ArgumentException(
                $"Unknown compression engine: {engine}")
        };
    }
}