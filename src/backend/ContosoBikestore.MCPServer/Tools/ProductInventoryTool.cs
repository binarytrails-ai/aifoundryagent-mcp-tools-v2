using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using ContosoBikestore.MCPServer.Models;

namespace ContosoBikestore.MCPServer.Tools;

[McpServerToolType]
public sealed class ProductInventoryTool
{
    private readonly HttpClient _client;
    private readonly ILogger<ProductInventoryTool> _logger;
    private readonly string _baseUrl;

    public ProductInventoryTool(HttpClient client, ILogger<ProductInventoryTool> logger)
    {
        _client = client;
        _logger = logger;
        _baseUrl = Environment.GetEnvironmentVariable("CONTOSO_STORE_URL") ??
            "https://contosoagent-contoso-store-mlylcr.azurewebsites.net";
    }

    [McpServerTool, Description("Get all available bikes from the Contoso bike store.")]
    public async Task<string> GetAvailableBikes()
    {
        try
        {
            var requestUri = $"{_baseUrl}/api/bikes";
            var response = await _client.GetAsync(requestUri);
            _logger.LogInformation("[ProductInventory] API Response: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ProductInventory] API call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return $"Failed to get bikes data: {response.StatusCode} - {response.ReasonPhrase}";
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("[ProductInventory] JSON Response: {JsonContent}", jsonContent);
            using var jsonDocument = JsonDocument.Parse(jsonContent);

            return JsonSerializer.Serialize(jsonDocument.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Network error in GetAvailableBikes: {Message}", ex.Message);
            return $"{{\"error\": \"Network error: {ex.Message}\"}}";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Request timeout in GetAvailableBikes");
            return $"{{\"error\": \"Request timeout: {ex.Message}\"}}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductInventory] Exception occurred in GetAvailableBikes");
            return $"{{\"error\": \"Internal error: {ex.Message}\"}}";
        }
    }

    [McpServerTool, Description("Get details for a specific bike by its ID.")]
    public async Task<string> GetBikeById(
        [Description("The ID of the bike to retrieve")] int bikeId)
    {
        try
        {
            var requestUri = $"{_baseUrl}/api/bikes/{bikeId}";
            var response = await _client.GetAsync(requestUri);
            _logger.LogInformation("[ProductInventory] API Response: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ProductInventory] API call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return $"Failed to get bike with ID {bikeId}: {response.StatusCode} - {response.ReasonPhrase}";
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("[ProductInventory] JSON Response: {JsonContent}", jsonContent);
            using var jsonDocument = JsonDocument.Parse(jsonContent);

            return JsonSerializer.Serialize(jsonDocument.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Network error in GetBikeById: {Message}", ex.Message);
            return $"{{\"error\": \"Network error: {ex.Message}\"}}";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Request timeout in GetBikeById");
            return $"{{\"error\": \"Request timeout: {ex.Message}\"}}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductInventory] Exception occurred in GetBikeById");
            return $"{{\"error\": \"Internal error: {ex.Message}\"}}";
        }
    }

    [McpServerTool, Description("Get bike ID by its name.")]
    public async Task<string> GetBikeIdByName(
        [Description("The name of the bike to find")] string bikeName)
    {
        try
        {
            var requestUri = $"{_baseUrl}/api/bikes";
            var response = await _client.GetAsync(requestUri);
            _logger.LogInformation("[ProductInventory] API Response: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ProductInventory] API call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return $"Failed to get bikes data: {response.StatusCode} - {response.ReasonPhrase}";
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("[ProductInventory] JSON Response: {JsonContent}", jsonContent);
            
            var bikes = JsonSerializer.Deserialize<List<BikeCatalogItem>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (bikes == null)
            {
                return "{\"error\": \"Failed to deserialize bikes data\"}";
            }

            var bike = bikes.FirstOrDefault(b => 
                b.ProductName.Equals(bikeName, StringComparison.OrdinalIgnoreCase));

            if (bike != null)
            {
                return JsonSerializer.Serialize(new
                {
                    id = bike.Id,
                    productName = bike.ProductName,
                    priceUSD = bike.PriceUSD
                });
            }

            return $"{{\"error\": \"No bike found with name '{bikeName}'\"}}";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Network error in GetBikeIdByName: {Message}", ex.Message);
            return $"{{\"error\": \"Network error: {ex.Message}\"}}";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Request timeout in GetBikeIdByName");
            return $"{{\"error\": \"Request timeout: {ex.Message}\"}}";
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[ProductInventory] JSON parsing error in GetBikeIdByName");
            return $"{{\"error\": \"Invalid response format: {ex.Message}\"}}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductInventory] Exception occurred in GetBikeIdByName");
            return $"{{\"error\": \"Internal error: {ex.Message}\"}}";
        }
    }

    [McpServerTool, Description("Get full details for a bike by its name or ID, including price, description, and stock information.")]
    public async Task<string> GetBikeDetails(
        [Description("The name or ID of the bike to find (e.g., 'Contoso Mountain X1' or '2')")] string bikeNameOrId)
    {
        try
        {
            // Check if input is a number (ID) or string (name)
            if (int.TryParse(bikeNameOrId, out int bikeId))
            {
                // It's an ID, use GetBikeById
                return await GetBikeById(bikeId);
            }

            // It's a name, search for it
            var requestUri = $"{_baseUrl}/api/bikes";
            var response = await _client.GetAsync(requestUri);
            _logger.LogInformation("[ProductInventory] API Response: {StatusCode} {ReasonPhrase}", response.StatusCode, response.ReasonPhrase);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[ProductInventory] API call failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return $"Failed to get bikes data: {response.StatusCode} - {response.ReasonPhrase}";
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogTrace("[ProductInventory] JSON Response: {JsonContent}", jsonContent);
            
            var bikes = JsonSerializer.Deserialize<List<BikeCatalogItem>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (bikes == null)
            {
                return "{\"error\": \"Failed to deserialize bikes data\"}";
            }

            var bike = bikes.FirstOrDefault(b => 
                b.ProductName.Equals(bikeNameOrId, StringComparison.OrdinalIgnoreCase));

            if (bike != null)
            {
                return JsonSerializer.Serialize(bike, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
            }

            return $"{{\"error\": \"No bike found with name '{bikeNameOrId}'\"}}";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Network error in GetBikeDetails: {Message}", ex.Message);
            return $"{{\"error\": \"Network error: {ex.Message}\"}}";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "[ProductInventory] Request timeout in GetBikeDetails");
            return $"{{\"error\": \"Request timeout: {ex.Message}\"}}";
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[ProductInventory] JSON parsing error in GetBikeDetails");
            return $"{{\"error\": \"Invalid response format: {ex.Message}\"}}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProductInventory] Exception occurred in GetBikeDetails");
            return $"{{\"error\": \"Internal error: {ex.Message}\"}}";
        }
    }
}