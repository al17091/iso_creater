using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiscUtils.Iso9660;

namespace iso_creater
{
    public enum IsoFileSystem
    {
        Iso9660,
        Joliet,
        Udf
    }

    public class IsoCreationService
    {
        public async Task CreateIsoAsync(
            string sourceDirectory,
            string outputIsoFile,
            string volumeLabel,
            IsoFileSystem fileSystem,
            IEnumerable<string> excludePatterns,
            IProgress<double> progress,
            CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory)) throw new ArgumentException("ソースディレクトリは必須です。", nameof(sourceDirectory));
            if (string.IsNullOrWhiteSpace(outputIsoFile)) throw new ArgumentException("出力ファイルは必須です。", nameof(outputIsoFile));
            if (!Directory.Exists(sourceDirectory)) throw new DirectoryNotFoundException($"指定されたソースディレクトリが見つかりません: {sourceDirectory}");

            var filesToInclude = GetFilesToInclude(sourceDirectory, excludePatterns);
            int totalFiles = filesToInclude.Count;
            int processedFiles = 0;

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var outDir = Path.GetDirectoryName(outputIsoFile);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                var builder = new CDBuilder
                {
                    VolumeIdentifier = volumeLabel,
                    UseJoliet = fileSystem != IsoFileSystem.Iso9660 // Use Joliet for both Joliet and UDF
                };

                foreach (var filePath in filesToInclude)
                {
                    token.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
                    builder.AddFile(relativePath, filePath);

                    processedFiles++;
                    if (totalFiles > 0)
                    {
                        progress?.Report((double)processedFiles / totalFiles * 100);
                    }
                }

                token.ThrowIfCancellationRequested();
                using var isoStream = File.Create(outputIsoFile);
                builder.Build(isoStream);

            }, token);
        }

        private List<string> GetFilesToInclude(string sourceDirectory, IEnumerable<string> excludePatterns)
        {
            var allFiles = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            
            if (excludePatterns == null || !excludePatterns.Any())
            {
                return allFiles.ToList();
            }

            var ignoreList = excludePatterns
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => DotNet.Globbing.Glob.Parse(p, new DotNet.Globbing.GlobOptions { Evaluation = { CaseInsensitive = true } }))
                .ToList();

            return allFiles.Where(file =>
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file).Replace(Path.DirectorySeparatorChar, '/');
                return !ignoreList.Any(glob => glob.IsMatch(relativePath));
            }).ToList();
        }
    }
}
