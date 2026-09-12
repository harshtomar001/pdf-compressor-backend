using Microsoft.AspNetCore.Http.Features;
using pdf_compressor.Hubs;
using pdf_compressor.Services.Compression;
using pdf_compressor.Services.Jobs;
using pdf_compressor.Services.Queue;
using pdf_compressor.Services.Storage;
using pdf_compressor.Workers;
using pdf_compressor.Configuration;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.Services.Configure<PdfToolOptions>(
            builder.Configuration.GetSection("PdfTools")
        );
        
        builder.Services.AddOpenApi();

        builder.Services.AddControllers();
        
        builder.Services.AddSingleton<GhostscriptEngine>();
        builder.Services.AddSingleton<MuPdfEngine>();
        builder.Services.AddSingleton<QPdfEngine>();
        // only one object for entire application
        
        builder.Services.AddSingleton<CompressionRouter>();
        
        builder.Services.AddSingleton<IPdfQueue, PdfQueue>();
        builder.Services.AddSingleton<IJobService, JobService>();
        
        builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
        
        builder.Services.AddSignalR();
        builder.Services.AddHostedService<PdfWorker>();
        
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 600 * 1024 * 1024;
            options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
            options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
        });

        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 600L * 1024 * 1024;
        });


        builder.WebHost.UseUrls("http://0.0.0.0:6464");

        var app = builder.Build(); 
        app.MapControllers();
        app.MapHub<PdfHub>("/PdfHub");
       

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.Run();
    }
}


