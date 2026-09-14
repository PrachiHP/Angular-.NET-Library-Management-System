using BackendAPI.Exceptions;

namespace BackendAPI.Services;

public class LocalFileStorageService : IFileStorageService
{
    /// <summary>
    /// Allowlist, never a blocklist. A blocklist can always be bypassed by an
    /// extension nobody thought to ban; an allowlist fails closed.
    /// HashSet for O(1) lookup and case-insensitive comparison.
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IWebHostEnvironment environment, ILogger<LocalFileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveAsync(
        IFormFile file,
        string subfolder,
        long maxBytes,
        CancellationToken ct = default)
    {
        if (file.Length == 0)
        {
            throw new ValidationException("The uploaded file is empty.");
        }

        if (file.Length > maxBytes)
        {
            throw new ValidationException(
                "File exceeds the " + (maxBytes / 1024 / 1024) + " MB limit.");
        }

        // FileInfo inspects the name without touching the disk.
        var info = new FileInfo(file.FileName);
        var extension = info.Extension.ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            throw new ValidationException(
                "Only " + string.Join(", ", AllowedExtensions) + " files are allowed.");
        }

        // NEVER reuse the client's filename. "../../appsettings.json" would be a
        // path-traversal attack, and two users uploading "cover.jpg" would
        // overwrite each other. A GUID solves both.
        var safeName = Guid.NewGuid().ToString("N") + extension;

        var webRoot = _environment.WebRootPath
                      ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var targetDir = Path.Combine(webRoot, "uploads", subfolder);

        // DirectoryInfo creates the folder only if it is missing.
        var dir = new DirectoryInfo(targetDir);
        if (!dir.Exists)
        {
            dir.Create();
        }

        var fullPath = Path.Combine(targetDir, safeName);

        try
        {
            // 'await using' disposes the stream even if CopyToAsync throws,
            // releasing the file handle. Without it the handle leaks and the
            // file stays locked.
            await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await file.CopyToAsync(stream, ct);
        }
        catch (Exception ex)
        {
            // Do not leave a half-written file behind.
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            // Inner exception preserved so the real IO error survives in logs.
            throw new AppExceptionWrapper("Failed to save the uploaded file.", ex);
        }

        var relative = "/uploads/" + subfolder + "/" + safeName;
        _logger.LogInformation("Saved upload {Path} ({Bytes} bytes)", relative, file.Length);

        return relative;
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var webRoot = _environment.WebRootPath
                      ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var fullPath = Path.Combine(webRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        // Guard against a stored path that escapes the uploads folder.
        var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
        if (!Path.GetFullPath(fullPath).StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Refused to delete path outside uploads: {Path}", relativePath);
            return;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted upload {Path}", relativePath);
        }
    }
}

/// <summary>Concrete AppException for infrastructure failures that should surface as 500.</summary>
public class AppExceptionWrapper : Exceptions.AppException
{
    public override int StatusCode => StatusCodes.Status500InternalServerError;

    public AppExceptionWrapper(string message, Exception inner) : base(message, inner)
    {
    }
}
