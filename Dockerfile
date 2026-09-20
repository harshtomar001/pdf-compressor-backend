
# ================================
# Stage 1: Build
# ================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY . .

RUN dotnet restore

RUN dotnet publish pdf_compressor.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


# ================================
# Stage 2: Production Runtime
# ================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Create non-root application user
RUN useradd --create-home --shell /bin/bash appuser

# Install PDF compression tools
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ghostscript \
        mupdf-tools \
        qpdf \
        wget \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Create persistent PDF storage directory
RUN mkdir -p /app/PDF_folder \
    && chown -R appuser:appuser /app

# ASP.NET Core listens on port 8080
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/PdfCompressor/health || exit 1
    
# Run application as non-root user
USER appuser

ENTRYPOINT ["dotnet", "pdf_compressor.dll"]