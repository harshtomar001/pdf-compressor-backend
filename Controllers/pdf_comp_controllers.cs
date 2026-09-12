using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using pdf_compressor.Hubs;
using pdf_compressor.Models;
using pdf_compressor.Services.Jobs;
using pdf_compressor.Services.Queue;
using pdf_compressor.Services.Storage;

namespace pdf_compressor.Controllers
{
        
        [ApiController]
        [Route("[controller]")]
        
        public class PdfCompressorController: ControllerBase
        {
                
                private readonly IPdfQueue _queue;
                private readonly IJobService _jobService;   
                private readonly IHubContext<PdfHub> _hub;
                private readonly IFileStorage _fileStorage;

                public PdfCompressorController(
                        IPdfQueue _queue,
                        IJobService jobService,
                        IFileStorage fileStorage,
                        IHubContext<PdfHub> hub)
                {
                        this._queue = _queue;
                        this._hub = hub;
                        this._jobService = jobService;
                        this._fileStorage = fileStorage;
                }

                
                [HttpGet("hello")]
                public IActionResult HelloWorld()
                {

                        return Ok("Hello World!");
                }

                [HttpPost("compress_PDF")]
                public async Task<IActionResult> Compress([FromForm] CompressionRequest request)
                {
                        string inputPath;
                        string outputPath;
                        string jobId;
                        
                        Console.WriteLine("1. File received");
                        
                        (inputPath, outputPath, jobId) = 
                                await _fileStorage.CreateJobFilesAsync(request.File);
                        
                        Console.WriteLine("1. File path done");
                        
                        Console.WriteLine($"{inputPath} -> {outputPath} -> {jobId}");

                        PdfJob pdfJob = _jobService.CreateJob(
                                jobId,
                                inputPath,
                                outputPath,
                                request.Engine,
                                new CompressionOptions
                                {
                                        Profile = request.Profile,
                                }
                        );
                        
                        await _fileStorage.SaveJobAsync(pdfJob);

                        Console.WriteLine($"Saved job data for: {jobId}");

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
                        string? json = await _fileStorage.ReadJobAsync(jobId);

                        if (json == null)
                        {
                                return NotFound("Job not found");
                        }

                        PdfJob? job = JsonSerializer.Deserialize<PdfJob>(json);

                        if (job == null)
                        {
                                return BadRequest("Invalid job data");
                        }

                        if (job.Status != JobStatus.Completed)
                        {
                                return BadRequest(
                                        $"Job status: {job.Status}"
                                );
                        }

                        Response.OnCompleted(async () =>
                        {
                                try
                                {
                                        await _fileStorage.DeleteJobAsync(jobId);

                                        Console.WriteLine(
                                                $"Deleted job storage: {jobId}"
                                        );
                                }
                                catch (Exception e)
                                {
                                        Console.WriteLine(e);
                                }
                        });

                        return PhysicalFile(
                                job.OutputPath,
                                "application/pdf",
                                "compressed.pdf"
                        );
                }


        }
        
}