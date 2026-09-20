using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using pdf_compressor.Exceptions;
using pdf_compressor.Hubs;
using pdf_compressor.Models;
using pdf_compressor.Services.Jobs;
using pdf_compressor.Services.Queue;
using pdf_compressor.Services.Storage;
using pdf_compressor.Workers;
using pdf_compressor.Services.Monitoring;

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
                private readonly ILogger<PdfWorker> _logger;
                private readonly WorkerMetrics _workerMetrics;

                public PdfCompressorController(
                        IPdfQueue _queue,
                        IJobService jobService,
                        IFileStorage fileStorage,
                        IHubContext<PdfHub> hub,
                        ILogger<PdfWorker> logger,
                        WorkerMetrics workerMetrics,
                        IJobCancellationService jobCancellationService)
                {
                        this._queue = _queue;
                        this._hub = hub;
                        this._jobService = jobService;
                        this._fileStorage = fileStorage;
                        this._jobCancellationService = jobCancellationService;
                        _logger = logger;
                        _workerMetrics =  workerMetrics;
                }

                
                [HttpGet("hello")]
                public IActionResult HelloWorld()
                {
                        return Ok("Hello World!\n PDF COMPRESSION BACKEND");
                }
                
                [HttpGet("health")]
                [HttpHead("health")]
                public IActionResult Health()
                {
                        return Ok(new
                        {
                                status = "healthy"
                        });
                }

                [HttpPost("compress_PDF")]
                [EnableRateLimiting("compression")]
                public async Task<IActionResult> Compress(
                        [FromForm] CompressionRequest request)
                {
                        string inputPath;
                        string outputPath;
                        string jobId;
                        
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
                                _logger.LogError(
                                        ex,
                                        "File storage error while creating job files."
                                );
                                return StatusCode(500, new ApiError
                                {
                                        Error = "StorageError",
                                        Message = "The file could not be stored."
                                });
                                
                                
                        }

                        _logger.LogInformation(
                                "Compression request received. JobId: {JobId}, FileName: {FileName}, Engine: {Engine}, Profile: {Profile}",
                                jobId,
                                request.File.FileName,
                                request.Engine,
                                request.Profile
                        );
                        
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

                                _logger.LogInformation(
                                        "Job data saved. JobId: {JobId}",
                                        jobId
                                );
                        }
                        catch (Exception ex)
                        {
                                _logger.LogError(
                                        ex,
                                        "Failed to save job data. JobId: {JobId}",
                                        jobId
                                );

                                try
                                {
                                        await _fileStorage.DeleteJobAsync(jobId);
                                        _logger.LogInformation(
                                                "Cleaned up failed job storage. JobId: {JobId}",
                                                jobId
                                        );
                                }
                                catch (Exception cleanupException)
                                {
                                        _logger.LogWarning(
                                                cleanupException,
                                                "Failed to clean up job storage. JobId: {JobId}",
                                                jobId
                                        );
                                }

                                return StatusCode(500, new ApiError
                                {
                                        Error = "StorageError",
                                        Message = "The job could not be saved."
                                });
                        }

                        _jobService.AddJob(pdfJob);
                        _queue.Enqueue(pdfJob);

                        _logger.LogInformation(
                                "Compression job queued. JobId: {JobId}, Position: {Position}",
                                jobId,
                                _queue.GetPosition(jobId)
                        );
                        
                        return Ok(new
                        {
                                jobId = jobId,
                                accessToken = accessToken,
                                position = _queue.GetPosition(jobId) +1
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

                [HttpGet("metrics")]
                public IActionResult Metrics()
                {
                        return Ok(new
                        {
                                activeJobs = _workerMetrics.ActiveJobs,
                                completedJobs = _workerMetrics.CompletedJobs,
                                failedJobs = _workerMetrics.FailedJobs,
                                cancelledJobs = _workerMetrics.CancelledJobs
                        });
                }
                
        }
        
}