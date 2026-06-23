using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DirectoryCopier.Services
{
    public class CopyProgress
    {
        public int TotalFiles { get; set; }
        public int CopiedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long CopiedBytes { get; set; }
        public string CurrentFile { get; set; } = "";
        public bool IsCompleted { get; set; }
        public string? Error { get; set; }
    }

    public class CopyEngine
    {
        public static (List<string> files, long totalBytes) ScanDirectory(string sourcePath)
        {
            var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).ToList();
            long totalBytes = 0;
            foreach (var file in files)
            {
                try
                {
                    totalBytes += new FileInfo(file).Length;
                }
                catch { /* ignore inaccessible files */ }
            }
            return (files, totalBytes);
        }

        public static async Task CopyDirectoryAsync(
            string sourcePath,
            string destinationPath,
            int threadCount,
            IProgress<CopyProgress> progress,
            CancellationToken cancellationToken)
        {
            var (files, totalBytes) = ScanDirectory(sourcePath);

            int copiedFiles = 0;
            long copiedBytes = 0;
            var lockObj = new object();

            Directory.CreateDirectory(destinationPath);

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, threadCount),
                CancellationToken = cancellationToken
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(files, options, file =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relativePath = Path.GetRelativePath(sourcePath, file);
                    string destFile = Path.Combine(destinationPath, relativePath);
                    string? destDir = Path.GetDirectoryName(destFile);

                    if (destDir != null)
                        Directory.CreateDirectory(destDir);

                    long fileSize = 0;
                    try
                    {
                        fileSize = new FileInfo(file).Length;
                        File.Copy(file, destFile, true);
                    }
                    catch (Exception ex)
                    {
                        progress.Report(new CopyProgress
                        {
                            TotalFiles = files.Count,
                            TotalBytes = totalBytes,
                            CurrentFile = relativePath,
                            Error = ex.Message
                        });
                        return;
                    }

                    lock (lockObj)
                    {
                        copiedFiles++;
                        copiedBytes += fileSize;
                        progress.Report(new CopyProgress
                        {
                            TotalFiles = files.Count,
                            CopiedFiles = copiedFiles,
                            TotalBytes = totalBytes,
                            CopiedBytes = copiedBytes,
                            CurrentFile = relativePath
                        });
                    }
                });
            }, cancellationToken);

            progress.Report(new CopyProgress
            {
                TotalFiles = files.Count,
                CopiedFiles = copiedFiles,
                TotalBytes = totalBytes,
                CopiedBytes = copiedBytes,
                IsCompleted = true
            });
        }
    }
}
