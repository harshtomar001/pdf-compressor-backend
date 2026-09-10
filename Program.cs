using Microsoft.AspNetCore.Http.Features;
using pdf_compressor.Hubs;
using pdf_compressor.Service;
using pdf_compressor.Workers;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddOpenApi();

        builder.Services.AddControllers();
        builder.Services.AddSingleton<GhostscriptService>(); // only one object for entire application
        builder.Services.AddSingleton<MuPdfService>();
        builder.Services.AddSingleton<QPdfService>(); 
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<PdfQueueService>();
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


