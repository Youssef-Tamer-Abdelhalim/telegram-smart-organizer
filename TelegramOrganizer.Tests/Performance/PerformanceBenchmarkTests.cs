using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;
using Xunit;
using Xunit.Abstractions;

namespace TelegramOrganizer.Tests.Performance
{
    /// <summary>
    /// Performance benchmark tests for V2.0 features.
    /// These tests establish baseline metrics for:
    /// - File organization latency
    /// - Database operations
    /// - Memory usage
    /// - Batch download handling
    /// </summary>
    public class PerformanceBenchmarkTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly SQLiteDatabaseService _databaseService;
        private readonly string _testDatabasePath;

        // Performance thresholds
        private const int MaxBatchOrganizationTimeMs = 5000; // 5 seconds for 100 files
        private const long MaxDatabaseSizeFor1000Files = 2 * 1024 * 1024; // 2 MB
        private const int MaxSingleFileOperationMs = 50; // 50ms per file operation

        public PerformanceBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
            
            // Create unique database path for benchmarks
            var testId = Guid.NewGuid().ToString("N")[..8];
            var testFolder = Path.Combine(AppContext.BaseDirectory, "BenchmarkDatabases");
            
            if (!Directory.Exists(testFolder))
            {
                Directory.CreateDirectory(testFolder);
            }
            
            _testDatabasePath = Path.Combine(testFolder, $"benchmark_{testId}.db");
            _databaseService = new SQLiteDatabaseService(_testDatabasePath);
        }

        public async Task InitializeAsync()
        {
            await _databaseService.InitializeDatabaseAsync();
        }

        public Task DisposeAsync()
        {
            _databaseService.CloseConnection();
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            try
            {
                if (File.Exists(_testDatabasePath))
                {
                    File.Delete(_testDatabasePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Benchmark: Simulates organizing 100 files in batch.
        /// Target: < 5 seconds total
        /// </summary>
        [Fact]
        public async Task Benchmark_BatchDownload_100Files()
        {
            // Arrange
            const int fileCount = 100;
            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            
            // Create a session
            var session = await _databaseService.CreateSessionAsync("Benchmark Group", "Test Window", "Benchmark");
            
            // Add files to session
            for (int i = 0; i < fileCount; i++)
            {
                await _databaseService.AddFileToSessionAsync(
                    session.Id,
                    $"file_{i:D3}.pdf",
                    $"/path/to/file_{i:D3}.pdf",
                    1024 * (i + 1) // Varying file sizes
                );
            }
            
            // Record statistics for each file
            for (int i = 0; i < fileCount; i++)
            {
                await _databaseService.RecordFileStatisticAsync(
                    $"file_{i:D3}.pdf",
                    ".pdf",
                    1024 * (i + 1),
                    "Benchmark Group",
                    "BenchmarkFolder",
                    true, // batch download
                    session.Id,
                    null,
                    1.0
                );
            }
            
            // End session
            await _databaseService.EndSessionAsync(session.Id);
            
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Batch download of {fileCount} files: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average per file: {stopwatch.ElapsedMilliseconds / (double)fileCount:F2}ms");
            
            Assert.True(stopwatch.ElapsedMilliseconds < MaxBatchOrganizationTimeMs,
                $"Batch organization took {stopwatch.ElapsedMilliseconds}ms, expected < {MaxBatchOrganizationTimeMs}ms");
        }

        /// <summary>
        /// Benchmark: Database size after 1000 file records.
        /// Target: < 2 MB
        /// </summary>
        [Fact]
        public async Task Benchmark_DatabaseSize_1000Files()
        {
            // Arrange
            const int fileCount = 1000;
            var random = new Random(42); // Deterministic seed
            var groups = new[] { "Group A", "Group B", "Group C", "Work", "Personal", "Downloads" };
            var extensions = new[] { ".pdf", ".jpg", ".png", ".mp4", ".docx", ".zip" };

            // Act - Insert 1000 file records
            for (int i = 0; i < fileCount; i++)
            {
                await _databaseService.RecordFileStatisticAsync(
                    $"document_{i:D4}{extensions[random.Next(extensions.Length)]}",
                    extensions[random.Next(extensions.Length)],
                    random.Next(1024, 10 * 1024 * 1024), // 1KB to 10MB
                    groups[random.Next(groups.Length)],
                    $"Folder_{random.Next(10)}",
                    random.Next(2) == 0, // 50% batch downloads
                    null,
                    null,
                    0.5 + random.NextDouble() * 0.5 // 0.5 to 1.0 confidence
                );
            }

            // Also add some patterns
            for (int i = 0; i < 50; i++)
            {
                await _databaseService.SavePatternAsync(new FilePattern
                {
                    FileExtension = extensions[random.Next(extensions.Length)],
                    GroupName = groups[random.Next(groups.Length)],
                    ConfidenceScore = 0.5 + random.NextDouble() * 0.5,
                    TimesSeen = random.Next(1, 100),
                    TimesCorrect = random.Next(1, 100),
                    FirstSeen = DateTime.Now.AddDays(-random.Next(30)),
                    LastSeen = DateTime.Now
                });
            }

            // Get database size
            var databaseSize = await _databaseService.GetDatabaseSizeAsync();

            // Assert
            _output.WriteLine($"Database size after {fileCount} files: {databaseSize / 1024.0:F2} KB");
            _output.WriteLine($"Average per file: {databaseSize / (double)fileCount:F2} bytes");
            
            Assert.True(databaseSize < MaxDatabaseSizeFor1000Files,
                $"Database size is {databaseSize / 1024.0:F2} KB, expected < {MaxDatabaseSizeFor1000Files / 1024.0:F2} KB");
        }

        /// <summary>
        /// Benchmark: Single file statistics recording.
        /// Target: < 50ms per operation
        /// </summary>
        [Fact]
        public async Task Benchmark_SingleFileOperation_Latency()
        {
            // Arrange
            const int iterations = 100;
            var stopwatch = new Stopwatch();
            var totalMs = 0L;

            // Act - Measure individual operations
            for (int i = 0; i < iterations; i++)
            {
                stopwatch.Restart();
                
                await _databaseService.RecordFileStatisticAsync(
                    $"test_file_{i}.pdf",
                    ".pdf",
                    1024,
                    "Test Group",
                    "TestFolder",
                    false,
                    null,
                    null,
                    1.0
                );
                
                stopwatch.Stop();
                totalMs += stopwatch.ElapsedMilliseconds;
            }

            var averageMs = totalMs / (double)iterations;

            // Assert
            _output.WriteLine($"Single file operation average: {averageMs:F2}ms over {iterations} iterations");
            _output.WriteLine($"Total time: {totalMs}ms");
            
            Assert.True(averageMs < MaxSingleFileOperationMs,
                $"Average operation took {averageMs:F2}ms, expected < {MaxSingleFileOperationMs}ms");
        }

        /// <summary>
        /// Benchmark: Session creation and management.
        /// Target: Fast session operations
        /// </summary>
        [Fact]
        public async Task Benchmark_SessionManagement()
        {
            // Arrange
            const int sessionCount = 50;
            const int filesPerSession = 10;
            var stopwatch = new Stopwatch();

            // Act
            stopwatch.Start();
            
            for (int s = 0; s < sessionCount; s++)
            {
                var session = await _databaseService.CreateSessionAsync(
                    $"Session_{s} Group",
                    $"Window_{s}",
                    "Telegram"
                );
                
                for (int f = 0; f < filesPerSession; f++)
                {
                    await _databaseService.AddFileToSessionAsync(
                        session.Id,
                        $"session_{s}_file_{f}.pdf",
                        null,
                        1024
                    );
                }
                
                await _databaseService.EndSessionAsync(session.Id);
            }
            
            stopwatch.Stop();

            // Assert
            var totalFiles = sessionCount * filesPerSession;
            _output.WriteLine($"Created {sessionCount} sessions with {totalFiles} files in {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average per session: {stopwatch.ElapsedMilliseconds / (double)sessionCount:F2}ms");
            
            Assert.True(stopwatch.ElapsedMilliseconds < 10000,
                $"Session management took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");
        }

        /// <summary>
        /// Benchmark: Pattern matching performance.
        /// Target: Fast pattern lookup
        /// </summary>
        [Fact]
        public async Task Benchmark_PatternMatching()
        {
            // Arrange - Create patterns
            var extensions = new[] { ".pdf", ".jpg", ".png", ".mp4", ".docx" };
            var groups = new[] { "Documents", "Images", "Videos", "Work" };
            
            for (int i = 0; i < 100; i++)
            {
                await _databaseService.SavePatternAsync(new FilePattern
                {
                    FileExtension = extensions[i % extensions.Length],
                    GroupName = groups[i % groups.Length],
                    ConfidenceScore = 0.5 + (i % 50) / 100.0,
                    TimesSeen = i + 1,
                    TimesCorrect = i,
                    FirstSeen = DateTime.Now.AddDays(-30),
                    LastSeen = DateTime.Now
                });
            }

            // Act - Measure pattern lookup
            const int lookupCount = 100;
            var stopwatch = new Stopwatch();
            
            stopwatch.Start();
            
            for (int i = 0; i < lookupCount; i++)
            {
                var best = await _databaseService.GetBestPatternAsync(
                    $"document_{i}.pdf",
                    ".pdf",
                    DateTime.Now
                );
            }
            
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Pattern lookup {lookupCount} times: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Average per lookup: {stopwatch.ElapsedMilliseconds / (double)lookupCount:F2}ms");
            
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"Pattern matching took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
        }

        /// <summary>
        /// Benchmark: Statistics retrieval performance.
        /// Target: Fast statistics queries
        /// </summary>
        [Fact]
        public async Task Benchmark_StatisticsRetrieval()
        {
            // Arrange - Add test data
            for (int i = 0; i < 500; i++)
            {
                await _databaseService.RecordFileStatisticAsync(
                    $"file_{i}.pdf",
                    ".pdf",
                    1024 * i,
                    $"Group_{i % 10}",
                    "TestFolder",
                    i % 2 == 0,
                    null,
                    null,
                    1.0
                );
            }

            // Act - Measure statistics retrieval
            var stopwatch = new Stopwatch();
            
            stopwatch.Start();
            
            var total = await _databaseService.GetTotalFilesOrganizedAsync();
            var topGroups = await _databaseService.GetTopGroupsAsync(10);
            var fileTypes = await _databaseService.GetFileTypeDistributionAsync();
            var dailyActivity = await _databaseService.GetDailyActivityAsync(30);
            var batchStats = await _databaseService.GetBatchDownloadStatsAsync();
            var totalSize = await _databaseService.GetTotalSizeOrganizedAsync();
            
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Statistics retrieval: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"  - Total files: {total}");
            _output.WriteLine($"  - Top groups: {topGroups.Count}");
            _output.WriteLine($"  - File types: {fileTypes.Count}");
            _output.WriteLine($"  - Daily activity days: {dailyActivity.Count}");
            _output.WriteLine($"  - Batch percentage: {batchStats.batchPercentage:F1}%");
            _output.WriteLine($"  - Total size: {totalSize / 1024.0:F2} KB");
            
            Assert.True(stopwatch.ElapsedMilliseconds < 500,
                $"Statistics retrieval took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
        }

        /// <summary>
        /// Benchmark: Context cache performance.
        /// Target: Fast cache operations
        /// </summary>
        [Fact]
        public async Task Benchmark_ContextCache()
        {
            // Arrange
            const int cacheEntries = 100;
            var stopwatch = new Stopwatch();

            // Act - Write to cache
            stopwatch.Start();
            
            for (int i = 0; i < cacheEntries; i++)
            {
                await _databaseService.SaveContextCacheAsync(
                    $"Window Title {i} - Telegram",
                    $"Group_{i}",
                    "Telegram",
                    0.9
                );
            }
            
            var writeTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();

            // Read from cache
            for (int i = 0; i < cacheEntries; i++)
            {
                var cached = await _databaseService.GetCachedContextAsync($"Window Title {i} - Telegram");
            }
            
            var readTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Stop();

            // Assert
            _output.WriteLine($"Context cache write ({cacheEntries} entries): {writeTime}ms");
            _output.WriteLine($"Context cache read ({cacheEntries} entries): {readTime}ms");
            _output.WriteLine($"Average write: {writeTime / (double)cacheEntries:F2}ms");
            _output.WriteLine($"Average read: {readTime / (double)cacheEntries:F2}ms");
            
            Assert.True(writeTime < 2000, $"Cache write took {writeTime}ms, expected < 2000ms");
            Assert.True(readTime < 1000, $"Cache read took {readTime}ms, expected < 1000ms");
        }

        /// <summary>
        /// Benchmark: Database maintenance operations.
        /// Target: Maintenance should complete quickly
        /// </summary>
        [Fact]
        public async Task Benchmark_DatabaseMaintenance()
        {
            // Arrange - Add some data
            for (int i = 0; i < 100; i++)
            {
                await _databaseService.RecordFileStatisticAsync(
                    $"file_{i}.pdf", ".pdf", 1024, "TestGroup", "TestFolder", false);
            }

            // Act
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            await _databaseService.RunMaintenanceAsync();
            
            stopwatch.Stop();

            // Also check integrity
            var integrityStopwatch = new Stopwatch();
            integrityStopwatch.Start();
            
            var isValid = await _databaseService.CheckIntegrityAsync();
            
            integrityStopwatch.Stop();

            // Assert
            _output.WriteLine($"Maintenance (VACUUM + ANALYZE): {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Integrity check: {integrityStopwatch.ElapsedMilliseconds}ms");
            
            Assert.True(isValid, "Database integrity check failed");
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
                $"Maintenance took {stopwatch.ElapsedMilliseconds}ms, expected < 5000ms");
        }
    }
}
