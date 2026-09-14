namespace BackendAPI.Services;

/// <summary>
/// Abstraction over where uploaded files live. Behind an interface so swapping
/// local disk for Azure Blob is one line in Program.cs and no change to any
/// service that saves a file — the Strategy pattern, applied through DI.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Saves the upload and returns the public relative path.</summary>
    Task<string> SaveAsync(IFormFile file, string subfolder, long maxBytes, CancellationToken ct = default);

    /// <summary>Removes a previously saved file. Safe to call if it is already gone.</summary>
    void Delete(string? relativePath);
}
