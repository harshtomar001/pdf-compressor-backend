using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
                private readonly IJobCancellationService _jobCancellationService;

                public PdfCompressorController(
                        IPdfQueue _queue,
                        IJobService jobService,
                        IFileStorage fileStorage,
                        IHubContext<PdfHub> hub,
                        IJobCancellationService jobCancellationService)
                {
                        this._queue = _queue;
                        this._hub = hub;
                        this._jobService = jobService;
                        this._fileStorage = fileStorage;
                        this._jobCancellationService = jobCancellationService;
                }

                
                [HttpGet("hello")]
                public IActionResult HelloWorld()
                {
                        return Ok("Hello World!\n PDF COMPRESSION BACKEND");
                }

                [HttpPost("compress_PDF")]
                [EnableRateLimiting("compression")]
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
                       

                        var (pdfJob, accessToken) = _jobService.CreateJob(
                                jobId,
                                inputPath,
                                outputPath,
                                request.Engine,
                                new CompressionOptions
                                {
                                        Profile = request.Profile,
                                }
                        );
                        
                        try
                        {
                                await _fileStorage.SaveJobAsync(pdfJob);

                                Console.WriteLine(
                                        $"Saved job data for: {jobId}"
                                );
                        }
                        catch (Exception ex)
                        {
                                Console.WriteLine(
                                        $"Failed to save job data: {ex}"
                                );

                                try
                                {
                                        await _fileStorage.DeleteJobAsync(jobId);

                                        Console.WriteLine(
                                                $"Cleaned up failed job storage: {jobId}"
                                        );
                                }
                                catch (Exception cleanupException)
                                {
                                        Console.WriteLine(
                                                $"Failed to clean up job storage: {jobId}"
                                        );

                                        Console.WriteLine(cleanupException);
                                }

                                return StatusCode(500, new ApiError
                                {
                                        Error = "StorageError",
                                        Message = "The job could not be saved."
                                });
                        }

                        _jobService.AddJob(pdfJob);
                        _queue.Enqueue(pdfJob);

                        Console.WriteLine(
                                $"Queued job: {jobId}"
                        );
                        
                        return Ok(new
                        {
                                jobId = jobId,
                                accessToken = accessToken,
                                position = _queue.GetPosition(jobId)
                        });
                }

                
                [HttpGet("get_compress_PDF")]
                public async Task<IActionResult> GetCompress(
                        string jobId,
                        [FromQuery] string accessToken)
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
                        
                        if (!_jobService.ValidateAccessToken(
                                    job,
                                    accessToken))
                        {
                                return Unauthorized(new ApiError
                                {
                                        Error = "InvalidAccessToken",
                                        Message = "Invalid or missing access token."
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
                public async Task<IActionResult> GetStatus(
                        string jobId,
                        [FromQuery] string accessToken)
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
                        
                        if (!_jobService.ValidateAccessToken(
                                    job,
                                    accessToken))
                        {
                                return Unauthorized(new ApiError
                                {
                                        Error = "InvalidAccessToken",
                                        Message = "Invalid or missing access token."
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
                
                [HttpPost("cancel/{jobId}")]
                public async Task<IActionResult> CancelJob(
                        string jobId,
                        [FromQuery] string accessToken)
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

                        if (!_jobService.ValidateAccessToken(
                                    job,
                                    accessToken))
                        {
                                return Unauthorized(new ApiError
                                {
                                        Error = "InvalidAccessToken",
                                        Message = "Invalid or missing access token."
                                });
                        }
                        
                        var cancelled = _jobCancellationService.Cancel(jobId);

                        if (!cancelled)
                        {
                                return NotFound(new ApiError
                                {
                                        Error = "JobNotCancellable",
                                        Message = "Job is not currently running or cannot be cancelled."
                                });
                        }

                        return Ok(new
                        {
                                jobId,
                                message = "Job cancellation requested."
                        });
                }

        }
        
}