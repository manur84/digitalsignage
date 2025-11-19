using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Extensions.Logging;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for generating thumbnails from media files (images, videos, PDFs)
/// </summary>
public class ThumbnailService
{
    private readonly ILogger<ThumbnailService> _logger;
    private readonly string _thumbnailDirectory;
    private const int ThumbnailWidth = 200;
    private const int ThumbnailHeight = 200;

    public ThumbnailService(ILogger<ThumbnailService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _thumbnailDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigitalSignage",
            "Thumbnails");

        Directory.CreateDirectory(_thumbnailDirectory);
        _logger.LogInformation("Thumbnail directory initialized: {Directory}", _thumbnailDirectory);
    }

    /// <summary>
    /// Generates a thumbnail for an image file
    /// </summary>
    /// <param name="sourceFilePath">Path to the source image file</param>
    /// <param name="originalFileName">Original filename (used to generate thumbnail name)</param>
    /// <returns>Path to the generated thumbnail, or null if generation failed</returns>
    public string? GenerateImageThumbnail(string sourceFilePath, string originalFileName)
    {
        // ✅ FIX: Use try-finally to ensure all GDI+ resources are disposed even on exceptions
        Image? sourceImage = null;
        Bitmap? thumbnail = null;
        Graphics? graphics = null;

        try
        {
            if (!File.Exists(sourceFilePath))
            {
                _logger.LogWarning("Source file not found: {FilePath}", sourceFilePath);
                return null;
            }

            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();

            // Skip SVG files (vector graphics don't need thumbnails, or require special handling)
            if (extension == ".svg")
            {
                _logger.LogInformation("Skipping thumbnail generation for SVG file: {FileName}", originalFileName);
                return null;
            }

            sourceImage = Image.FromFile(sourceFilePath);

            // Calculate thumbnail dimensions maintaining aspect ratio
            var (thumbnailWidth, thumbnailHeight) = CalculateThumbnailSize(
                sourceImage.Width,
                sourceImage.Height,
                ThumbnailWidth,
                ThumbnailHeight);

            // Create thumbnail with high quality
            thumbnail = new Bitmap(thumbnailWidth, thumbnailHeight);
            graphics = Graphics.FromImage(thumbnail);

            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            // Fill background with white
            graphics.Clear(Color.White);

            // Draw the source image scaled to fit
            graphics.DrawImage(sourceImage, 0, 0, thumbnailWidth, thumbnailHeight);

            // Dispose graphics before saving
            graphics.Dispose();
            graphics = null;

            // Generate unique thumbnail filename
            var thumbnailFileName = $"thumb_{Path.GetFileNameWithoutExtension(originalFileName)}_{Guid.NewGuid():N}.jpg";
            var thumbnailPath = Path.Combine(_thumbnailDirectory, thumbnailFileName);

            // Save as JPEG with 90% quality
            var encoder = Encoder.Quality;
            using var encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(encoder, 90L);

            var jpegCodec = GetEncoderInfo("image/jpeg");
            if (jpegCodec != null)
            {
                thumbnail.Save(thumbnailPath, jpegCodec, encoderParameters);
            }
            else
            {
                // Fallback to standard JPEG save
                thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);
            }

            _logger.LogInformation("Thumbnail generated: {ThumbnailPath} ({Width}x{Height})",
                thumbnailPath, thumbnailWidth, thumbnailHeight);

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate thumbnail for: {FileName}", originalFileName);
            return null;
        }
        finally
        {
            // Ensure all GDI+ resources are disposed
            graphics?.Dispose();
            thumbnail?.Dispose();
            sourceImage?.Dispose();
        }
    }

    /// <summary>
    /// Generates a placeholder thumbnail for videos
    /// In the future, this can extract the first frame using FFmpeg
    /// </summary>
    /// <param name="sourceFilePath">Path to the source video file</param>
    /// <param name="originalFileName">Original filename</param>
    /// <returns>Path to the generated placeholder thumbnail</returns>
    public string? GenerateVideoThumbnail(string sourceFilePath, string originalFileName)
    {
        try
        {
            // For now, create a placeholder with a video icon
            // TODO: Use FFmpeg to extract first frame
            var thumbnailFileName = $"thumb_{Path.GetFileNameWithoutExtension(originalFileName)}_{Guid.NewGuid():N}.jpg";
            var thumbnailPath = Path.Combine(_thumbnailDirectory, thumbnailFileName);

            using var thumbnail = new Bitmap(ThumbnailWidth, ThumbnailHeight);
            using (var graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(Color.FromArgb(240, 240, 240));

                // Draw video icon placeholder
                using var brush = new SolidBrush(Color.FromArgb(100, 100, 100));
                using var font = new Font("Segoe UI", 14, FontStyle.Bold);
                var text = "VIDEO";
                var textSize = graphics.MeasureString(text, font);

                graphics.DrawString(
                    text,
                    font,
                    brush,
                    (ThumbnailWidth - textSize.Width) / 2,
                    (ThumbnailHeight - textSize.Height) / 2);
            }

            thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);

            _logger.LogInformation("Video placeholder thumbnail generated: {ThumbnailPath}", thumbnailPath);

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate video thumbnail for: {FileName}", originalFileName);
            return null;
        }
    }

    /// <summary>
    /// Generates a placeholder thumbnail for documents
    /// </summary>
    /// <param name="sourceFilePath">Path to the source document file</param>
    /// <param name="originalFileName">Original filename</param>
    /// <returns>Path to the generated placeholder thumbnail</returns>
    public string? GenerateDocumentThumbnail(string sourceFilePath, string originalFileName)
    {
        try
        {
            var extension = Path.GetExtension(originalFileName).ToUpperInvariant().TrimStart('.');

            var thumbnailFileName = $"thumb_{Path.GetFileNameWithoutExtension(originalFileName)}_{Guid.NewGuid():N}.jpg";
            var thumbnailPath = Path.Combine(_thumbnailDirectory, thumbnailFileName);

            using var thumbnail = new Bitmap(ThumbnailWidth, ThumbnailHeight);
            using (var graphics = Graphics.FromImage(thumbnail))
            {
                graphics.Clear(Color.White);

                // Draw document icon with extension text
                using var borderBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
                using var fillBrush = new SolidBrush(Color.FromArgb(250, 250, 250));
                using var textBrush = new SolidBrush(Color.FromArgb(80, 80, 80));
                using var extensionBrush = new SolidBrush(Color.FromArgb(0, 120, 215));
                using var font = new Font("Segoe UI", 12, FontStyle.Bold);
                using var extensionFont = new Font("Segoe UI", 20, FontStyle.Bold);

                // Draw document rectangle
                var docRect = new Rectangle(40, 20, ThumbnailWidth - 80, ThumbnailHeight - 40);
                graphics.FillRectangle(fillBrush, docRect);

                // ✅ FIX: Dispose Pen properly to avoid GDI+ handle leak
                using var borderPen = new Pen(borderBrush, 2);
                graphics.DrawRectangle(borderPen, docRect);

                // Draw extension
                var extText = extension;
                var extSize = graphics.MeasureString(extText, extensionFont);
                graphics.DrawString(
                    extText,
                    extensionFont,
                    extensionBrush,
                    (ThumbnailWidth - extSize.Width) / 2,
                    (ThumbnailHeight - extSize.Height) / 2);
            }

            thumbnail.Save(thumbnailPath, ImageFormat.Jpeg);

            _logger.LogInformation("Document placeholder thumbnail generated: {ThumbnailPath}", thumbnailPath);

            return thumbnailPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate document thumbnail for: {FileName}", originalFileName);
            return null;
        }
    }

    /// <summary>
    /// Deletes a thumbnail file if it exists
    /// </summary>
    /// <param name="thumbnailPath">Path to the thumbnail to delete</param>
    public void DeleteThumbnail(string? thumbnailPath)
    {
        if (string.IsNullOrEmpty(thumbnailPath))
            return;

        try
        {
            if (File.Exists(thumbnailPath))
            {
                File.Delete(thumbnailPath);
                _logger.LogInformation("Thumbnail deleted: {ThumbnailPath}", thumbnailPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete thumbnail: {ThumbnailPath}", thumbnailPath);
        }
    }

    /// <summary>
    /// Calculates thumbnail size maintaining aspect ratio
    /// </summary>
    private (int width, int height) CalculateThumbnailSize(
        int sourceWidth,
        int sourceHeight,
        int maxWidth,
        int maxHeight)
    {
        var aspectRatio = (double)sourceWidth / sourceHeight;

        int targetWidth, targetHeight;

        if (aspectRatio > 1.0)
        {
            // Landscape
            targetWidth = maxWidth;
            targetHeight = (int)(maxWidth / aspectRatio);
        }
        else
        {
            // Portrait or square
            targetHeight = maxHeight;
            targetWidth = (int)(maxHeight * aspectRatio);
        }

        return (targetWidth, targetHeight);
    }

    /// <summary>
    /// Gets the JPEG codec encoder
    /// </summary>
    private ImageCodecInfo? GetEncoderInfo(string mimeType)
    {
        var encoders = ImageCodecInfo.GetImageEncoders();
        return encoders.FirstOrDefault(e => e.MimeType == mimeType);
    }
}
