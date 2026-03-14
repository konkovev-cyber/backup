using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;
using BackupSystem.Core;

namespace BackupSystem.Archiver;

public class SharpZipArchiver : IArchiver
{
    public async Task<string> ArchiveAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        ArchiverSettings settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Ensure extension is correct
        if (!destinationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            destinationPath += ".zip";
        }

        await Task.Run(() =>
        {
            using var fsOut = File.Create(destinationPath);
            using var zipStream = new ZipOutputStream(fsOut);

            // Set compression level (0-9)
            zipStream.SetLevel(GetCompressionLevel(settings.CompressionLevel));

            if (!string.IsNullOrEmpty(settings.Password))
            {
                zipStream.Password = settings.Password;
            }

            var paths = sourcePaths.ToList();
            var totalFiles = paths.Count; // This is a simplification
            var processedFiles = 0;

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(path))
                {
                    AddFileToZip(zipStream, path, Path.GetFileName(path));
                    processedFiles++;
                    progress?.Report((double)processedFiles / totalFiles * 100);
                }
                else if (Directory.Exists(path))
                {
                    AddDirectoryToZip(zipStream, path, "", cancellationToken);
                    // Progress reporting for directories is more complex, skipping for simplicity
                }
            }

            zipStream.Finish();
            zipStream.Close();
        }, cancellationToken);

        return destinationPath;
    }

    public async Task<bool> VerifyAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var zf = new ZipFile(archivePath);
                if (!string.IsNullOrEmpty(password))
                {
                    zf.Password = password;
                }

                foreach (ZipEntry entry in zf)
                {
                    if (!entry.IsFile) continue;
                    using var s = zf.GetInputStream(entry);
                    // Just read to end to verify CRC
                    byte[] buffer = new byte[4096];
                    StreamUtils.Copy(s, Stream.Null, buffer);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private void AddFileToZip(ZipOutputStream zipStream, string filePath, string entryName)
    {
        var entry = new ZipEntry(entryName)
        {
            DateTime = File.GetLastWriteTime(filePath),
            Size = new FileInfo(filePath).Length
        };
        zipStream.PutNextEntry(entry);

        using var fs = File.OpenRead(filePath);
        byte[] buffer = new byte[4096];
        StreamUtils.Copy(fs, zipStream, buffer);
        zipStream.CloseEntry();
    }

    private void AddDirectoryToZip(ZipOutputStream zipStream, string dirPath, string relativePath, CancellationToken token)
    {
        var files = Directory.GetFiles(dirPath);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            var entryName = Path.Combine(relativePath, Path.GetFileName(file));
            AddFileToZip(zipStream, file, entryName);
        }

        var dirs = Directory.GetDirectories(dirPath);
        foreach (var dir in dirs)
        {
            token.ThrowIfCancellationRequested();
            var dirName = Path.GetFileName(dir);
            AddDirectoryToZip(zipStream, dir, Path.Combine(relativePath, dirName), token);
        }
    }

    private int GetCompressionLevel(string level) => level?.ToLower() switch
    {
        "none" => 0,
        "fast" => 1,
        "normal" => 5,
        "max" => 8,
        "ultra" => 9,
        _ => 5
    };
}
