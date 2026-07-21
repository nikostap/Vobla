namespace Marketplace.Web.Modules.Storage;

public static class UploadTemporaryFileCleanup
{
    public static int RemoveOlderThan(IEnumerable<string> roots, DateTimeOffset cutoff)
    {
        var removed = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var configuredRoot in roots)
        {
            var root = Path.GetFullPath(configuredRoot);
            if (!Directory.Exists(root)) continue;
            var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            foreach (var candidate in Directory.EnumerateFiles(root, ".*.uploading", options))
            {
                var fullPath = Path.GetFullPath(candidate);
                if (!fullPath.StartsWith(rootPrefix, StringComparison.Ordinal) || File.GetLastWriteTimeUtc(fullPath) >= cutoff.UtcDateTime) continue;
                try { File.Delete(fullPath); removed++; }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return removed;
    }
}
