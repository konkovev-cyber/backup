using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Readers;
using BackupSystem.Core;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace BackupSystem.Archiver;

public class MultiFormatArchiver : IArchiver
{
    public async Task<string> ArchiveAsync(
        IEnumerable<string> sourcePaths,
        string destinationPath,
        ArchiverSettings settings,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var format = settings.Format?.ToLower() ?? "zip";
        
        // Ensure extension
        if (!destinationPath.EndsWith("." + format, StringComparison.OrdinalIgnoreCase))
        {
            destinationPath += "." + format;
        }

        await Task.Run(() =>
        {
            if (format == "zip" && !string.IsNullOrEmpty(settings.Password))
            {
                // ZIP with Password - use SharpZipLib as it handles it gracefully
                CreateZipWithPassword(sourcePaths, destinationPath, settings, progress, cancellationToken);
            }
            else
            {
                // 7z, Tar or plain Zip - use SharpCompress
                CreateSharpCompressArchive(sourcePaths, destinationPath, settings, progress, cancellationToken);
            }
        }, cancellationToken);

        return destinationPath;
    }

    private void CreateZipWithPassword(IEnumerable<string> sourcePaths, string destinationPath, ArchiverSettings settings, IProgress<double>? progress, CancellationToken token)
    {
        using var fsOut = File.Create(destinationPath);
        using var zipStream = new ZipOutputStream(fsOut);
        zipStream.SetLevel(GetCompressionLevelInt(settings.CompressionLevel));
        zipStream.Password = settings.Password;

        var paths = sourcePaths.ToList();
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                AddFileToZip(zipStream, path, Path.GetFileName(path));
            }
            else if (Directory.Exists(path))
            {
                AddDirectoryToZip(zipStream, path, Path.GetFileName(path), token);
            }
        }
    }

    private void CreateSharpCompressArchive(IEnumerable<string> sourcePaths, string destinationPath, ArchiverSettings settings, IProgress<double>? progress, CancellationToken token)
    {
        var archiveType = (settings.Format?.ToLower()) switch
        {
            "7z" => ArchiveType.SevenZip,
            "tar" => ArchiveType.Tar,
            _ => ArchiveType.Zip
        };

        var writerOptions = new WriterOptions(GetCompressionType(settings.CompressionLevel))
        {
            LeaveStreamOpen = false
        };

        using var fs = File.Create(destinationPath);
        using var writer = SharpCompress.Writers.WriterFactory.Open(fs, archiveType, writerOptions);

        var paths = sourcePaths.ToList();
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                writer.Write(Path.GetFileName(path), path);
            }
            else if (Directory.Exists(path))
            {
                AddDirectoryToWriter(writer, path, Path.GetFileName(path), token);
            }
        }
    }

    public async Task<bool> VerifyAsync(string archivePath, string? password = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using (var archive = SharpCompress.Archives.ArchiveFactory.Open(archivePath, new SharpCompress.Readers.ReaderOptions { Password = password }))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!entry.IsDirectory)
                        {
                            using var s = entry.OpenEntryStream();
                            byte[] buffer = new byte[4096];
                            while (s.Read(buffer, 0, buffer.Length) > 0) { }
                        }
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private void AddDirectoryToWriter(IWriter writer, string dirPath, string relativePath, CancellationToken token)
    {
        foreach (var file in Directory.GetFiles(dirPath))
        {
            token.ThrowIfCancellationRequested();
            writer.Write(Path.Combine(relativePath, Path.GetFileName(file)), file);
        }
        foreach (var dir in Directory.GetDirectories(dirPath))
        {
            token.ThrowIfCancellationRequested();
            AddDirectoryToWriter(writer, dir, Path.Combine(relativePath, Path.GetFileName(dir)), token);
        }
    }

    private void AddFileToZip(ZipOutputStream zipStream, string filePath, string entryName)
    {
        var entry = new ZipEntry(entryName) { DateTime = File.GetLastWriteTime(filePath), Size = new FileInfo(filePath).Length };
        zipStream.PutNextEntry(entry);
        using var fs = File.OpenRead(filePath);
        fs.CopyTo(zipStream);
        zipStream.CloseEntry();
    }

    private void AddDirectoryToZip(ZipOutputStream zipStream, string dirPath, string relativePath, CancellationToken token)
    {
        foreach (var file in Directory.GetFiles(dirPath))
        {
            token.ThrowIfCancellationRequested();
            AddFileToZip(zipStream, file, Path.Combine(relativePath, Path.GetFileName(file)));
        }
        foreach (var dir in Directory.GetDirectories(dirPath))
        {
            token.ThrowIfCancellationRequested();
            AddDirectoryToZip(zipStream, dir, Path.Combine(relativePath, Path.GetFileName(dir)), token);
        }
    }

    private CompressionType GetCompressionType(string level) => level?.ToLower() switch
    {
        "none" => CompressionType.None,
        "max" => CompressionType.BZip2,
        "ultra" => CompressionType.LZMA,
        _ => CompressionType.Deflate
    };

    private int GetCompressionLevelInt(string level) => level?.ToLower() switch
    {
        "none" => 0,
        "fast" => 1,
        "max" => 8,
        "ultra" => 9,
        _ => 5
    };
}
