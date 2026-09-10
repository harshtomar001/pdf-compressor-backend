namespace pdf_compressor;

public class handleRequest_file
{
    public static async Task<(string inputPath,string outputPath ,string jobId)> handleFile(IFormFile file)
    {
        var fileName = file.FileName;
        
        if (file == null || file.Length == 0)
        {
            throw new Exception("Corrupted file");
        }

        string fileExtension = Path.GetExtension(fileName);

        if (fileExtension != ".pdf")
        {
            throw new Exception("Only PDF files are allowed");
        }
        
        
        var uniqueName=Guid.NewGuid().ToString();
        

        string uniqueFolder =Path.Combine("PDF_folder",uniqueName);
        
        Directory.CreateDirectory(uniqueFolder);

        string inputPath = Path.GetFullPath(Path.Combine(uniqueFolder, "input.pdf"));

         using (var stream = new FileStream(inputPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        
        string outputPath=Path.GetFullPath(Path.Combine(uniqueFolder, "output.pdf"));


        return (inputPath, outputPath,uniqueName);

    }

}