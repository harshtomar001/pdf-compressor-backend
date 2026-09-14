using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using pdf_compressor.Exceptions;
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
                        return Ok("Hello World!\n PDF COMPRESSION BACKEND");
                }

                [HttpPost("compress_PDF")]
                public async Task<IActionResult> Compress([FromForm] CompressionRequest request)
                {
                        string inputPath;
                        string outputPath;
                        string jobId;
                        
                        Console.WriteLine("1. File received");
                        
                        try
                        {
                                (inputPath, outputPath, jobId) =
                                        await _fileStorage.CreateJobFilesAsync(request.File);
                        }
                        catch (InvalidFileException ex)
                        {
                                return BadRequest(new ApiError
                                {
                                        Error = "InvalidFile",
                                        Message = ex.Message
                                });
                        }
                        catch (Exception ex)
                        {
                                Console.WriteLine(
                                        $"File storage error: {ex}"
                                );

                                return StatusCode(500, new ApiError
                                {
                                        Error = "StorageError",
                                        Message = "The file could not be stored."
                                });
                        }
                        
                        Console.WriteLine("1. File path done");
                        
                        Console.WriteLine($"{inputPath} -> {outputPath} -> {jobId}");
                        
                        Console.WriteLine($"Profile {request.Profile}");
                        Console.WriteLine($"Engine: {request.Engine}");
                       

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
                        PdfJob? job = await _jobService.GetJobAsync(jobId);

                        if (job == null)
                        {
                                return NotFound(new ApiError
                                {
                                        Error = "JobNotFound",
                                        Message = "Job not found."
                                });
                        }

                        if (job.Status != JobStatus.Completed)
                        {
                                return BadRequest(new ApiError
                                {
                                        Error = "JobNotCompleted",
                                        Message = $"Job status: {job.Status}."
                                });
                        }

                        Response.OnCompleted(async () =>
                        {
                                try
                                {
                                        await _jobService.RemoveJobAsync(jobId);

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
                
                
                [HttpGet("status/{jobId}")]
                public async Task<IActionResult> GetStatus(string jobId)
                {
                        PdfJob? job = await _jobService.GetJobAsync(jobId);

                        if (job == null)
                        {
                                return NotFound(new ApiError
                                {
                                        Error = "JobNotFound",
                                        Message = "Job not found."
                                });
                        }

                        if (job.Status == JobStatus.Failed)
                        {
                                return Ok(new
                                {
                                        jobId = job.JobId,
                                        status = job.Status.ToString(),
                                        engine = job.CompressionEngine,
                                        profile = job.Compression.Profile,
                                        error = "CompressionFailed",
                                        message = "PDF compression failed."
                                });
                        }

                        return Ok(new
                        {
                                jobId = job.JobId,
                                status = job.Status.ToString(),
                                engine = job.CompressionEngine,
                                profile = job.Compression.Profile
                        });
                }

        }
        
}