using System.IO;
using System.IO.Compression;
using System.Text;

namespace DigitalSignage.Server.Helpers;

/// <summary>
/// Helper class for gzip compression/decompression of WebSocket messages
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Minimum size in bytes for compression to be beneficial
    /// Messages smaller than this will not be compressed (overhead not worth it)
    /// </summary>
    public const int MIN_COMPRESSION_SIZE = 1024; // 1KB

    /// <summary>
    /// Compress a byte array using gzip
    /// </summary>
    /// <param name="data">Data to compress</param>
    /// <returns>Compressed data</returns>
    public static byte[]? Compress(byte[]? data)
    {
        if (data == null || data.Length == 0)
            return data;

        // Don't compress small messages (overhead not worth it)
        if (data.Length < MIN_COMPRESSION_SIZE)
            return data;

        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest))
        {
            gzipStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Compress a string using gzip
    /// </summary>
    /// <param name="text">Text to compress</param>
    /// <returns>Compressed data</returns>
    public static byte[] CompressString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<byte>();

        var bytes = Encoding.UTF8.GetBytes(text);
        return Compress(bytes) ?? Array.Empty<byte>();
    }

    /// <summary>
    /// Decompress gzip data to byte array
    /// </summary>
    /// <param name="compressedData">Compressed data</param>
    /// <returns>Decompressed data</returns>
    public static byte[]? Decompress(byte[]? compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return compressedData;

        using var inputStream = new MemoryStream(compressedData);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
        {
            gzipStream.CopyTo(outputStream);
        }
        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompress gzip data to string
    /// </summary>
    /// <param name="compressedData">Compressed data</param>
    /// <returns>Decompressed string</returns>
    public static string DecompressString(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return string.Empty;

        var decompressed = Decompress(compressedData);
        return decompressed != null ? Encoding.UTF8.GetString(decompressed) : string.Empty;
    }

    /// <summary>
    /// Check if data is compressed (starts with gzip header)
    /// </summary>
    /// <param name="data">Data to check</param>
    /// <returns>True if data appears to be gzip compressed</returns>
    public static bool IsCompressed(byte[] data)
    {
        if (data == null || data.Length < 2)
            return false;

        // gzip magic header: 0x1F 0x8B
        return data[0] == 0x1F && data[1] == 0x8B;
    }

    /// <summary>
    /// Calculate compression ratio (compressed size / original size)
    /// </summary>
    /// <param name="originalSize">Original data size</param>
    /// <param name="compressedSize">Compressed data size</param>
    /// <returns>Compression ratio (0.0 to 1.0, lower is better)</returns>
    public static double CalculateCompressionRatio(int originalSize, int compressedSize)
    {
        if (originalSize == 0)
            return 1.0;

        return (double)compressedSize / originalSize;
    }

    /// <summary>
    /// Check if compression would be beneficial for given data size
    /// </summary>
    /// <param name="dataSize">Size of data in bytes</param>
    /// <returns>True if compression is recommended</returns>
    public static bool ShouldCompress(int dataSize)
    {
        return dataSize >= MIN_COMPRESSION_SIZE;
    }
}
