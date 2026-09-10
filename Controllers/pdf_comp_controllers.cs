using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using pdf_compressor.Hubs;
using pdf_compressor.Models;
using pdf_compressor.Service;
using pdf_compressor.Services.Jobs;
using pdf_compressor.Services.Queue;

namespace pdf_compressor.Controllers
{
        
        [ApiController]
        [Route("[controller]")]
        
        public class PdfCompressorController: ControllerBase
        {
                
                private readonly IPdfQueue _queue;
                private readonly IJobService _jobService;   
                private readonly IHubContext<PdfHub> _hub;

                public PdfCompressorController(IPdfQueue _queue,IJobService jobService,IHubContext<PdfHub> hub)
                {
                        this._queue = _queue;
                        this._hub = hub;
                        this._jobService = jobService;
                }

                
                [HttpGet("hello")]
                public IActionResult HelloWorld()
                {

                        return Ok("Hello World!");
                }

                [HttpPost("compress_PDF")]
                public async Task<IActionResult> Compress( IFormFile file)
                {
                        string inputPath;
                        string outputPath;
                        string jobId;
                        
                        Console.WriteLine("1. File received");
                        
                        (inputPath, outputPath,jobId) = await handleRequest_file.handleFile(file);
                        
                        Console.WriteLine("1. File path done");
                        
                        Console.WriteLine($"{inputPath} -> {outputPath} -> {jobId}");

                        PdfJob pdfJob = new PdfJob
                        {
                                JobId = jobId,
                                InputPath = inputPath,
                                OutputPath = outputPath,
                                Status = "Queued"

                        };
                        
                        string json = JsonSerializer.Serialize(
                                pdfJob,
                                new JsonSerializerOptions
                                {
                                        WriteIndented = true
                                });
                        
                        Console.WriteLine(json);

                        string ? folderName=Path.GetDirectoryName(inputPath); // getting the folder name of the each request
                        
                        string? jobFile = Path.Combine(folderName, "job.json"); // creating the  json file  in subfolder of the request 

                        await System.IO.File.WriteAllTextAsync(jobFile, json);// writing data to job.json file 
                        
                        Console.WriteLine(jobFile);

                        _jobService.AddJob(pdfJob);
                        _queue.Enqueue(pdfJob);
                        
                        
                        return Ok(new
                        {
                                jobId = jobId,
                                position=_queue.GetPosition(jobId)
                        });

                }

                [HttpGet("get_compress_PDF")]
                public async Task<IActionResult> GetCompress(string jobId)
                {
                        PdfJob? job = _jobService.GetJob(jobId);

                        if (job == null)
                        {
                                return NotFound("Job not found");
                        }

                        if (job.Status != "Completed")
                        {
                                return BadRequest(
                                        $"Job status: {job.Status}"
                                );
                        }

                        string folder = Path.Combine("PDF_folder", jobId);

                        Response.OnCompleted(() =>
                        {
                                try
                                {
                                        Directory.Delete(folder, true);
                                        Console.WriteLine($"Deleted {folder}");
                                }
                                catch (Exception e)
                                {
                                        Console.WriteLine(e);
                                }

                                return Task.CompletedTask;
                        });

                        return PhysicalFile(
                                job.OutputPath,
                                "application/pdf",
                                "compressed.pdf"
                        );
                }


        }
        
}