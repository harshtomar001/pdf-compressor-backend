# Phase 6 — Performance & Benchmarking

## 1. Objective

The objective of Phase 6 was to measure and evaluate the performance of the PDF compression backend.

The benchmark focused on:

- Compression execution time
- Throughput
- Output size
- Compression ratio
- Queue concurrency
- Performance bottlenecks
- Ghostscript optimization
- Final production configuration

---

## 2. Test Environment

### Backend

- Framework: ASP.NET Core
- Language: C#
- Compression engine: Ghostscript
- Queue workers: 2 concurrent workers
- API endpoint:

```text
POST /PdfCompressor/compress_PDF
```

### Input PDF

- Input size: **169,831,416 bytes**
- Approximate size: **169.83 MB**
- Pages: **366**
- PDF version: **1.4**
- Images: **366**
- Image format: **JPEG / DCT**
- Color space: **DeviceRGB**
- Color depth: **8 bits per component**

The input document is primarily a raster/scanned PDF with one JPEG image per page.

---

## 3. Benchmark Methodology

The same input PDF was used for the benchmark tests.

The compression profile used for the final benchmark was:

```text
balanced
```

The selected compression engine was:

```text
ghostscript
```

The final Ghostscript configuration was intentionally kept simple:

```text
-sDEVICE=pdfwrite
-dCompatibilityLevel=1.4
-dNOPAUSE
-dQUIET
-dBATCH
-sOutputFile=<output>
<input>
```

Further image-processing optimizations were investigated separately and were not included in the final configuration.

---

## 4. Compression Time Benchmark

Earlier benchmark testing showed significant runtime variation between executions.

The Balanced profile had the following earlier results:

- Average: approximately **10.80 seconds**
- Median: approximately **8.26 seconds**
- Minimum: approximately **7.64 seconds**
- Maximum: approximately **21.01 seconds**

When the two approximately 21-second outliers were excluded:

- Average: approximately **8.26 seconds**
- Median: approximately **8.13 seconds**
- Standard deviation: approximately **0.45 seconds**

This demonstrated that Ghostscript execution time can vary significantly between individual runs.

---

## 5. Final Balanced Benchmark

Three final Balanced compression runs were performed using the production configuration.

| Run | Ghostscript Time | Compression Time | Output Size |
|---|---:|---:|---:|
| 1 | 16.807 s | 16.808 s | 58,661,382 B |
| 2 | 21.160 s | 21.160 s | 58,661,382 B |
| 3 | 7.919 s | 7.919 s | 58,661,382 B |

### Input

```text
169,831,416 bytes
```

### Output

```text
58,661,382 bytes
```

The output size was identical across all three final runs.

---

## 6. Final Benchmark Statistics

Based on the three final runs:

| Metric | Result |
|---|---:|
| Average compression time | **15.296 s** |
| Median compression time | **16.808 s** |
| Minimum | **7.919 s** |
| Maximum | **21.160 s** |
| Standard deviation | **~6.63 s** |

The benchmark demonstrates considerable execution-time variability.

However, the resulting PDF size remained identical across all three runs.

---

## 7. Throughput

Using the average final compression time:

```text
Throughput =
Input Size / Average Compression Time
```

Approximately:

```text
169.831 MB / 15.296 s
≈ 11.10 MB/s
```

Therefore, the final measured average throughput was approximately:

**11.10 MB/s**

The earlier normal Balanced runs, excluding major outliers, achieved approximately:

**20.56 MB/s**

---

## 8. Compression Ratio

Input:

```text
169,831,416 bytes
```

Output:

```text
58,661,382 bytes
```

Output/input ratio:

```text
58,661,382 / 169,831,416
≈ 34.53%
```

Therefore:

- Output is approximately **34.53%** of the original size.
- Size reduction is approximately **65.47%**.

The approximate amount of data removed is:

```text
169,831,416 - 58,661,382
= 111,170,034 bytes
```

Therefore, approximately **111.17 MB** of the original data was removed.

---

## 9. Compression Profile Comparison

Earlier testing was performed using Low, Balanced, and High profiles.

| Profile | Average Time | Median Time | Approx. Output |
|---|---:|---:|---:|
| Low | ~15.09 s | ~15.54 s | ~58.66 MB |
| Balanced | ~10.80 s | ~8.26 s | ~58.66 MB |
| High | ~17.65 s | ~18.49 s | ~58.69 MB |

The output sizes were extremely similar across the three profiles for this particular input PDF.

Balanced therefore provided the best overall performance among the tested profiles.

---

## 10. Queue Concurrency

The PDF worker was configured with two concurrent processing workers.

Two simultaneous PDF compression requests were tested.

Both jobs started within approximately **330 ms** of each other and completed within approximately **231 ms** of each other.

This confirmed that the queue is capable of processing two compression jobs concurrently.

The concurrency test was performed before the final clean Ghostscript configuration and was intended to verify queue concurrency rather than establish the final compression performance.

---

## 11. Bottleneck Investigation

Several measurements were performed to identify the primary performance bottleneck.

### CPU

Overall system CPU measurements were approximately:

```text
20% – 30%
```

These measurements were not sufficient to conclude that the system was CPU-bound because they represented overall system CPU utilization rather than Ghostscript process-specific utilization.

### Disk

Disk throughput measurements varied considerably during compression.

The measurements were not sufficient to establish that the system was disk-bound.

### Ghostscript Process

The Ghostscript process consumed the majority of the observed compression time.

Application-level overhead was comparatively small.

Therefore:

> **Ghostscript execution is the dominant latency component of the compression pipeline.**

---

## 12. Ghostscript Optimization Experiment

A controlled experiment tested:

```text
-dDetectDuplicateImages=true
```

The clean baseline produced approximately:

```text
Ghostscript: 8.209 s
Compression: 8.212 s
Output: 58,661,353 bytes
```

With duplicate-image detection enabled:

```text
Ghostscript: 8.952 s
Compression: 8.963 s
Output: 58,661,382 bytes
```

### Result

Enabling duplicate-image detection resulted in:

- Approximately **9% slower** compression
- No meaningful reduction in output size

Therefore:

```text
-dDetectDuplicateImages=true
```

was rejected for the current production configuration.

Further Ghostscript optimization was intentionally deferred.

---

## 13. Experimental Image Processing

An earlier experimental Ghostscript configuration attempted to control image resolution and JPEG quality using parameters such as:

```text
-dDownsampleColorImages=true
-dColorImageResolution=<DPI>
-dColorImageDownsampleType=/Average
-dAutoFilterColorImages=false
-dColorImageFilter=/DCTEncode
-dJPEGQ=<quality>
```

The experiment produced significantly smaller files, approximately:

```text
30,581,295 bytes
```

However, it also increased compression time substantially.

For example, the Balanced configuration took approximately:

```text
23.896 seconds
```

compared with the clean baseline of approximately:

```text
8.212 seconds
```

The experimental configuration therefore reduced output size at the cost of significantly higher processing time.

The experiment was not selected as the final production configuration.

Further image-quality and compression optimization has been deferred for future work.

---

## 14. Final Ghostscript Configuration

The final configuration is:

```text
-sDEVICE=pdfwrite
-dCompatibilityLevel=1.4
-dNOPAUSE
-dQUIET
-dBATCH
-sOutputFile=<output>
<input>
```

No experimental image-processing parameters are currently enabled.

This configuration was selected because it provides a simple and stable baseline without adding processing overhead that did not demonstrate sufficient benefit for the current implementation.

---

## 15. Final Performance Conclusion

Phase 6 established the performance characteristics of the PDF compression backend.

For the tested **169.83 MB** raster PDF:

- Typical Balanced compression can complete in roughly **8–21 seconds**.
- Final three-run average was approximately **15.30 seconds**.
- Final average throughput was approximately **11.10 MB/s**.
- Output size was approximately **58.66 MB**.
- Size reduction was approximately **65.47%**.
- Two simultaneous queue workers were successfully verified.
- Ghostscript is the dominant source of compression latency.
- Application overhead is comparatively small.
- Duplicate-image detection did not provide a useful performance/size benefit.
- More aggressive image processing significantly reduced file size but increased processing time and was therefore deferred.

The current implementation provides a stable performance baseline suitable for continuing development.

---

## 16. Future Optimization Opportunities

Further optimization has intentionally been deferred.

Possible future work includes:

- More detailed Ghostscript profiling
- Process-specific CPU measurements
- Controlled disk I/O profiling
- Ghostscript parameter benchmarking
- Parallelism tuning
- Memory usage profiling
- Large-scale load testing
- Different PDF workload benchmarks
- Engine-specific performance comparison between Ghostscript, MuPDF, and QPDF
- Image-quality versus compression-size benchmarking
- Evaluation of additional Ghostscript image downsampling settings

These optimizations are not required for the current phase.

---

# Phase 6 Status

## COMPLETE

**Phase 6 — Performance & Benchmarking**

The backend now has a documented performance baseline and identified performance characteristics for future optimization work.

### Final Baseline

```text
Input:
169,831,416 bytes (~169.83 MB)

Output:
58,661,382 bytes (~58.66 MB)

Average compression time:
15.296 seconds

Average throughput:
~11.10 MB/s

Size reduction:
~65.47%

Queue concurrency:
2 workers verified

Primary bottleneck:
Ghostscript execution
```

Further performance optimization is intentionally deferred to a future phase.