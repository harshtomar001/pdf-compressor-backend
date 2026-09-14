namespace pdf_compressor.Exceptions;

public class InvalidFileException : Exception
{
    public InvalidFileException(string message)
        : base(message)
    {
    }
}