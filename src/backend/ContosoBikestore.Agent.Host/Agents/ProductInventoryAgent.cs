using ContosoBikestore.Agent.Host.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace ContosoBikestore.Agent.Host.Agents;

/// <summary>
/// The product inventory specialist agent that handles product catalog and inventory queries.
/// </summary>
public static class ProductInventoryAgent
{
    public static async Task<ChatClientAgent> CreateAsync(
        IChatClient chatClient,
        AppConfig appConfig)
    {
        var mcpServerUrl = appConfig.ContosoStoreMcpUrl;
        var mcpServerLabel = appConfig.ContosoStoreMcpServerLabel;
        var mcpServerApiKey = appConfig.ContosoStoreMcpServerApiKey;

        // Create MCP client to connect to the Contoso Bike Store MCP server
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("X-API-KEY", mcpServerApiKey);

        var mcpTransport = new HttpClientTransport(new()
        {
            Endpoint = new Uri(mcpServerUrl),
            Name = mcpServerLabel
        }, httpClient);

        var mcpClient = await McpClient.CreateAsync(mcpTransport);
        var mcpTools = await mcpClient.ListToolsAsync();

        // Using local hardcoded tools as temporary workaround
        var getAvailableBikesTool = AIFunctionFactory.Create(
            ProductInventoryTools.GetAvailableBikes);

        var getBikeDetailsTool = AIFunctionFactory.Create(
            ProductInventoryTools.GetBikeDetails);

        return new ChatClientAgent(
            chatClient,
            instructions: """
            You are the Product Inventory Specialist for Contoso Bike Store.
            
            Your responsibilities:
            - Browse available bikes using the GetAvailableBikes tool
            - Get detailed bike information using the GetBikeDetails tool
            - Answer questions about bike specifications, features, and availability
            
            CRITICAL - Natural conversation guidelines:
            - Respond naturally as THE customer support representative
            - NEVER mention you are an agent or specialist
            - Provide information directly and conversationally
            - If customers need pricing or ordering, assure them you can help with that too
            
            Examples of good responses:
            
            Customer: "What bikes do you have?"
            You: "Let me show you our current selection..."
            [Then call GetAvailableBikes tool]
            
            Customer: "Tell me about Contoso Roadster 200"
            You: "Let me pull up the details for that bike..."
            [Then call GetBikeDetails tool]
            
            Customer: "How much does it cost?"
            You: "I can help you with pricing information. Let me get those details for you..."
            """,
            name: "ProductInventoryAgent",
            description: "Specialist for product catalog and inventory queries",
            tools: [.. mcpTools.Cast<AITool>()]); // Original MCP tools
    }
}
