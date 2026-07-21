namespace Marketplace.Web.Modules.Storage;

public sealed record StoredUpload(string FileName, string FullPath);

public static class AtomicUploadStorage
{
    public static async Task<StoredUpload> SaveAsync(IFormFile file, string directory, string extension, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var finalPath = Path.Combine(directory, fileName);
        var temporaryPath = Path.Combine(directory, $".{fileName}.uploading");
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await file.CopyToAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, finalPath);
            return new StoredUpload(fileName, finalPath);
        }
        catch
        {
            TryDelete(temporaryPath);
            TryDelete(finalPath);
            throw;
        }
    }

    public static void Cleanup(IEnumerable<StoredUpload> files)
    {
        foreach (var file in files) TryDelete(file.FullPath);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* Best-effort compensation; the original failure remains primary. */ }
    }
}
