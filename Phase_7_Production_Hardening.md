# Phase 7 — Production Hardening

## Overview

Phase 7 focused on making the PDF compression backend reliable and safe
for single-server production deployment.

## Reliability

- Graceful application shutdown
- Safe queue shutdown
- Job cancellation
- Compression process termination
- Resource cleanup
- Job state recovery after unexpected termination
- Persistent job consistency
- Concurrent worker processing

## Storage Safety

- Per-job isolated storage
- Minimum free-disk-space checks
- Atomic job metadata writes
- Partial output cleanup
- Safe deletion with retry handling
- Orphaned job-folder cleanup
- Path traversal protection

## API Security

- Global IP-based rate limiting
- Compression-specific rate limiting
- Per-job access tokens
- SHA-256 token hashing
- Constant-time token comparison
- Protected status, download and cancellation endpoints
- Protected SignalR job groups

## Validation

- Required request fields
- Engine validation
- Compression-profile validation
- Structured API validation errors
- Production configuration validation at startup

## Observability

- Structured logging
- Job lifecycle logging
- Queue-position logging
- Compression success/failure logging
- Cleanup logging
- Storage-error logging

## Configuration

Development-specific PDF tool paths are stored in:

ppsettings.Development.json

Production deployments provide PDF tool paths through configuration
environment variables:

- PdfTools__Ghostscript
- PdfTools__MuPdf
- PdfTools__QPdf

Production startup validates the required PDF tool configuration.

## Testing

Reliability test results are documented in:

Phase_7_Reliability.md

Performance benchmark results are documented in:

Phase_6_Performance.md

## Phase Result

Phase 7 production hardening is complete.

The backend is prepared for the next stage:

**Phase 8 — Docker & Production Deployment**
