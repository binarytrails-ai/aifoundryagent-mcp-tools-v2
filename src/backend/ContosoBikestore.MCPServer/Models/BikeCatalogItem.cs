namespace ContosoBikestore.MCPServer.Models;

public class BikeCatalogItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceUSD { get; set; }
    public List<string> Tags { get; set; } = new();
    public string City { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public int StockAvailability { get; set; }
}
