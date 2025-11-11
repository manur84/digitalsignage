using System.IO;

namespace DigitalSignage.Server.Services;

public interface IMediaService
{
    Task<string> SaveMediaAsync(byte[] data, string fileName);
    Task<byte[]?> GetMediaAsync(string fileName);
    Task<bool> DeleteMediaAsync(string fileName);
    Task<List<string>> GetAllMediaFilesAsync();
}

public class MediaService : IMediaService
{
    private readonly string _mediaDirectory;

    public MediaService()
    {
        _mediaDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalSignage",
            "Media");

        Directory.CreateDirectory(_mediaDirectory);
    }

    public async Task<string> SaveMediaAsync(byte[] data, string fileName)
    {
        var filePath = Path.Combine(_mediaDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, data);
        return fileName;
    }

    public async Task<byte[]?> GetMediaAsync(string fileName)
    {
        var filePath = Path.Combine(_mediaDirectory, fileName);
        if (!File.Exists(filePath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(filePath);
    }

    public Task<bool> DeleteMediaAsync(string fileName)
    {
        var filePath = Path.Combine(_mediaDirectory, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<List<string>> GetAllMediaFilesAsync()
    {
        var files = Directory.GetFiles(_mediaDirectory)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Cast<string>()
            .ToList();

        return Task.FromResult(files);
    }
}
