#nullable enable

using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Handles layout assignment and distribution to clients
/// </summary>
internal class ClientLayoutDistributor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ClientLayoutDistributor> _logger;
    private readonly ICommunicationService _communicationService;
    private readonly ILayoutService _layoutService;
    private readonly ISqlDataService _dataService;
    private readonly IScribanService _scribanService;

    public ClientLayoutDistributor(
        IServiceProvider serviceProvider,
        ILogger<ClientLayoutDistributor> logger,
        ICommunicationService communicationService,
        ILayoutService layoutService,
        ISqlDataService dataService,
        IScribanService scribanService)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _communicationService = communicationService ?? throw new ArgumentNullException(nameof(communicationService));
        _layoutService = layoutService ?? throw new ArgumentNullException(nameof(layoutService));
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _scribanService = scribanService ?? throw new ArgumentNullException(nameof(scribanService));
    }

    /// <summary>
    /// Assigns a layout to a client and sends it immediately
    /// </summary>
    /// <param name="clientId">The ID of the client to assign the layout to. Must not be null or empty.</param>
    /// <param name="layoutId">The ID of the layout to assign. Must not be null or empty.</param>
    /// <param name="clientsCache">The in-memory cache of connected clients</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A result indicating success or failure with an error message</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Validates clientId and layoutId are not null/empty
    /// 2. Checks if client exists in cache
    /// 3. Updates the client's assigned layout in memory and database
    /// 4. Sends the layout to the client immediately
    /// </remarks>
    public async Task<Result> AssignLayoutAsync(
        string clientId,
        string layoutId,
        ConcurrentDictionary<string, RaspberryPiClient> clientsCache,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== AssignLayoutAsync START === ClientId: {ClientId}, LayoutId: {LayoutId}", clientId, layoutId);

        try
        {
            // Guard clause: Validate clientId
            if (string.IsNullOrWhiteSpace(clientId))
            {
                _logger.LogWarning("AssignLayoutAsync called with null or empty clientId");
                return Result.Failure("Client ID cannot be empty");
            }

            // Guard clause: Validate layoutId
            if (string.IsNullOrWhiteSpace(layoutId))
            {
                _logger.LogWarning("AssignLayoutAsync called with null or empty layoutId");
                return Result.Failure("Layout ID cannot be empty");
            }

            _logger.LogDebug("Checking if client {ClientId} exists in cache (have {Count} clients)", clientId, clientsCache.Count);

            if (clientsCache.TryGetValue(clientId, out var client))
            {
                client.AssignedLayoutId = layoutId;
                _logger.LogInformation("Assigned layout {LayoutId} to client {ClientId} in memory", layoutId, clientId);

                // Update in database
                try
                {
                    _logger.LogDebug("Updating database for client {ClientId}", clientId);
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<DigitalSignageDbContext>();

                    var dbClient = await dbContext.Clients.FindAsync(new object[] { clientId }, cancellationToken);
                    if (dbClient != null)
                    {
                        dbClient.AssignedLayoutId = layoutId;
                        await dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogDebug("Database updated successfully for client {ClientId}", clientId);
                    }
                    else
                    {
                        _logger.LogWarning("Client {ClientId} not found in database", clientId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update client {ClientId} layout assignment in database", clientId);
                }

                // Send layout update to client
                _logger.LogInformation("Sending layout {LayoutId} to client {ClientId}", layoutId, clientId);
                var result = await SendLayoutToClientAsync(clientId, layoutId, cancellationToken);
                _logger.LogInformation("=== AssignLayoutAsync END === Result: {Success}", result.IsSuccess ? "SUCCESS" : "FAILURE: " + result.ErrorMessage);
                return result;
            }

            _logger.LogWarning("Client {ClientId} not found for layout assignment (have {Count} clients in cache)",
                clientId, clientsCache.Count);
            _logger.LogInformation("=== AssignLayoutAsync END === Result: FAILURE (client not found)");
            return Result.Failure($"Client '{clientId}' not found");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Layout assignment cancelled for client {ClientId}", clientId);
            _logger.LogInformation("=== AssignLayoutAsync END === Result: CANCELLED");
            return Result.Failure("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign layout {LayoutId} to client {ClientId}", layoutId, clientId);
            _logger.LogInformation("=== AssignLayoutAsync END === Result: EXCEPTION");

            return Result.Failure($"Failed to assign layout: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sends a layout to a client with all data and embedded media
    /// </summary>
    /// <param name="clientId">The ID of the client to send the layout to</param>
    /// <param name="layoutId">The ID of the layout to send</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A result indicating success or failure</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Loads the layout from LayoutService (with null check)
    /// 2. Fetches data from all configured data sources
    /// 3. Processes Scriban templates in layout elements
    /// 4. Embeds media files (images) as Base64
    /// 5. Sends the complete layout via WebSocket
    /// </remarks>
    public async Task<Result> SendLayoutToClientAsync(
        string clientId,
        string layoutId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Load layout with null-safety check
            var layoutResult = await _layoutService.GetLayoutByIdAsync(layoutId, cancellationToken);
            if (layoutResult.IsFailure || layoutResult.Value == null)
            {
                _logger.LogError("Layout {LayoutId} not found: {ErrorMessage}", layoutId, layoutResult.ErrorMessage ?? "Null result");
                return Result.Failure($"Layout '{layoutId}' not found: {layoutResult.ErrorMessage ?? "Null result"}");
            }

            var layout = layoutResult.Value;

            // Fetch data from all data sources
            var layoutData = await FetchDataSourcesAsync(layout, cancellationToken);

            // Process templates in layout elements
            await ProcessTemplatesAsync(layout, layoutData, cancellationToken);

            // Embed media files (images) as Base64 for client transfer
            await EmbedMediaFilesInLayoutAsync(layout, cancellationToken);

            // Send standard display update without SQL data source payloads
            var displayUpdateMessage = new DisplayUpdateMessage
            {
                Layout = layout,
                Data = layoutData
            };

            await _communicationService.SendMessageAsync(clientId, displayUpdateMessage, cancellationToken);
            _logger.LogInformation("Sent DISPLAY_UPDATE with full layout {LayoutId} to client {ClientId}",
                layoutId, clientId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send layout update to client {ClientId}", clientId);
            return Result.Failure($"Failed to send layout to client: {ex.Message}", ex);
        }
    }

    private async Task<Dictionary<string, object>> FetchDataSourcesAsync(
        DisplayLayout layout,
        CancellationToken cancellationToken)
    {
        var layoutData = new Dictionary<string, object>();

        if (layout.DataSources == null || layout.DataSources.Count == 0)
        {
            return layoutData;
        }

        foreach (var dataSource in layout.DataSources)
        {
            try
            {
                var data = await _dataService.GetDataAsync(dataSource, cancellationToken);
                layoutData[dataSource.Id] = data;
                _logger.LogDebug("Loaded data for source {DataSourceId}", dataSource.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load data for source {DataSourceId}, using empty data", dataSource.Id);
                layoutData[dataSource.Id] = new Dictionary<string, object>();
            }
        }

        return layoutData;
    }

    private async Task ProcessTemplatesAsync(
        DisplayLayout layout,
        Dictionary<string, object> layoutData,
        CancellationToken cancellationToken)
    {
        // Flatten all data into a single dictionary for template processing
        var templateData = new Dictionary<string, object>();
        foreach (var kvp in layoutData)
        {
            if (kvp.Value is Dictionary<string, object> dict)
            {
                foreach (var dataKvp in dict)
                {
                    templateData[dataKvp.Key] = dataKvp.Value;
                }
            }
        }

        // Process text elements with templates
        if (layout.Elements == null || layout.Elements.Count == 0)
        {
            return;
        }

        foreach (var element in layout.Elements)
        {
            if (element.Type == "text")
            {
                try
                {
                    var content = element["Content"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        element["Content"] = await _scribanService.ProcessTemplateAsync(
                            content,
                            templateData,
                            cancellationToken);
                        _logger.LogDebug("Processed template for element {ElementId}", element.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process template for element {ElementId}, using original content", element.Id);
                }
            }
        }
    }

    /// <summary>
    /// Embed media files (images) as Base64 in layout elements for client transfer
    /// </summary>
    private async Task EmbedMediaFilesInLayoutAsync(DisplayLayout layout, CancellationToken cancellationToken)
    {
        if (layout?.Elements == null || layout.Elements.Count == 0)
        {
            _logger.LogDebug("No elements to process for media embedding in layout {LayoutId}", layout?.Id ?? "unknown");
            return;
        }

        // Get MediaService from DI container
        var mediaService = _serviceProvider.GetService<IMediaService>();
        if (mediaService == null)
        {
            _logger.LogWarning("MediaService not available, cannot embed media files in layout {LayoutId}", layout.Id);
            return;
        }

        int embedCount = 0;
        int errorCount = 0;

        // Process all image elements
        foreach (var element in layout.Elements.Where(e => e.Type?.ToLower() == "image"))
        {
            try
            {
                // Get the Source property (filename, path, or media ID)
                var source = element.GetProperty<string>("Source", "");
                if (string.IsNullOrEmpty(source))
                {
                    _logger.LogDebug("Image element {ElementId} has no Source property, skipping", element.Id);
                    continue;
                }

                // Extract filename (might be full path or just filename)
                var fileName = System.IO.Path.GetFileName(source);

                _logger.LogDebug("Attempting to load media file: {FileName} for element {ElementId}", fileName, element.Id);

                // Try to load media file from disk
                var imageResult = await mediaService.GetMediaAsync(fileName);
                if (imageResult.IsSuccess && imageResult.Value != null && imageResult.Value.Length > 0)
                {
                    var imageData = imageResult.Value;
                    // Embed as Base64 (use "MediaData" to match client expectations)
                    var base64 = Convert.ToBase64String(imageData);
                    element.SetProperty("MediaData", base64);

                    embedCount++;
                    _logger.LogInformation("✓ Embedded media file {FileName} ({Size} KB) as Base64 for element {ElementId}",
                        fileName, imageData.Length / 1024, element.Id);
                }
                else
                {
                    errorCount++;
                    _logger.LogWarning("✗ Media file not found or failed to load: {FileName} for element {ElementId} (Source: {Source}). Error: {Error}",
                        fileName,
                        element.Id,
                        source,
                        imageResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Failed to embed media file for image element {ElementId}", element.Id);
            }
        }

        if (embedCount > 0 || errorCount > 0)
        {
            _logger.LogInformation("Media embedding complete for layout {LayoutId}: {EmbedCount} embedded, {ErrorCount} errors",
                layout.Id, embedCount, errorCount);
        }
        else
        {
            _logger.LogDebug("No image elements found in layout {LayoutId}", layout.Id);
        }
    }
}
