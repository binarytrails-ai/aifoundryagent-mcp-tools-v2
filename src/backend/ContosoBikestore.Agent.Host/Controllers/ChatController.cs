using ContosoBikestore.Agent.Host.Models;
using Azure.AI.Agents.Persistent;
using Microsoft.AspNetCore.Mvc;

namespace ContosoBikestore.Agent.Host.Controllers
{
    [ApiController]
    [Route("api")]
    public class ChatController : ControllerBase
    {
        private readonly ILogger<ChatController> _logger;
        private readonly PersistentAgentsClient _persistentAgentsClient;
        private readonly AppConfig _appConfig;

        public ChatController(
            ILogger<ChatController> logger,
            PersistentAgentsClient persistentAgentsClient,
            AppConfig appConfig)
        {
            _logger = logger;
            _persistentAgentsClient = persistentAgentsClient;
            _appConfig = appConfig;
        }

        /// <summary>
        /// Gets chat history for a thread.
        /// </summary>
        [HttpGet("chat/history")]
        public async Task<IActionResult> GetChatHistory([FromQuery] string? threadId)
        {
            try
            {
                _logger.LogInformation("Fetching chat history for threadId: {ThreadId}", threadId);

                if (string.IsNullOrEmpty(threadId))
                {
                    _logger.LogInformation("No threadId provided, returning empty history.");
                    return Ok(new List<ChatMessageHistory>());
                }

                var thread = await _persistentAgentsClient.Threads.GetThreadAsync(threadId);
                if (thread == null || !thread.HasValue)
                {
                    _logger.LogWarning("No persistent thread found for threadId: {ThreadId}", threadId);
                    return Ok(new List<ChatMessageHistory>());
                }

                var messages = new List<ChatMessageHistory>();
                var threadMessages = _persistentAgentsClient.Messages.GetMessagesAsync(
                    threadId: thread.Value.Id,
                    order: ListSortOrder.Ascending);

                await foreach (var threadMessage in threadMessages)
                {
                    foreach (var contentItem in threadMessage.ContentItems)
                    {
                        if (contentItem is MessageTextContent textItem)
                        {
                            messages.Add(new ChatMessageHistory
                            {
                                Role = threadMessage.Role == MessageRole.User ? "user" : "assistant",
                                Content = textItem.Text,
                                CreatedAt = threadMessage.CreatedAt.ToString("o")
                            });
                        }
                    }
                }

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chat history for threadId: {ThreadId}", threadId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Sends a message to the agent.
        /// </summary>
        [HttpPost("chat/send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            try
            {
                _logger.LogInformation("Sending message to agent: {AgentName}", request.AgentName);

                // Get or create thread
                PersistentAgentThread thread;
                string threadId;
                if (!string.IsNullOrEmpty(request.ThreadId))
                {
                    try
                    {
                        var existingThread = await _persistentAgentsClient.Threads.GetThreadAsync(request.ThreadId);
                        if (existingThread.HasValue)
                        {
                            thread = existingThread.Value;
                            threadId = request.ThreadId;
                        }
                        else
                        {
                            var newThread = await _persistentAgentsClient.Threads.CreateThreadAsync();
                            thread = newThread.Value;
                            threadId = newThread.Value.Id.Replace("conv", "thread");
                        }
                    }
                    catch
                    {
                        var newThread = await _persistentAgentsClient.Threads.CreateThreadAsync();
                        thread = newThread.Value;
                        threadId = newThread.Value.Id.Replace("conv", "thread");
                    }
                }
                else
                {
                    var newThread = await _persistentAgentsClient.Threads.CreateThreadAsync();
                    thread = newThread.Value;
                    threadId = newThread.Value.Id.Replace("conv", "thread");
                }

                // Get or find the agent
                Microsoft.Agents.AI.AIAgent agent;
                if (!string.IsNullOrEmpty(request.AgentId))
                {
                    try
                    {
                        agent = await _persistentAgentsClient.GetAIAgentAsync(request.AgentId);
                    }
                    catch
                    {
                        // If agent ID is invalid, find by name
                        agent = await FindAgentByNameAsync(request.AgentName);
                    }
                }
                else
                {
                    agent = await FindAgentByNameAsync(request.AgentName);
                }

                // Create the message
                _persistentAgentsClient.Messages.CreateMessage(
                    thread.Id, MessageRole.User, request.Message);

                // Setup MCP tool resources
                var mcpServerLabel = _appConfig.ContosoStoreMcpServerLabel;
                MCPToolResource mcpToolResource = new(mcpServerLabel);
                ToolResources toolResources = mcpToolResource.ToToolResources();

                // Get the agent as PersistentAgent for CreateRun
                var persistentAgent = await _persistentAgentsClient.Administration.GetAgentAsync(agent.Id);

                // Run the agent
                ThreadRun run = _persistentAgentsClient.Runs.CreateRun(thread, persistentAgent.Value, toolResources);

                // Wait for completion with tool approval handling
                while (run.Status == RunStatus.Queued ||
                    run.Status == RunStatus.InProgress ||
                    run.Status == RunStatus.RequiresAction)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    run = _persistentAgentsClient.Runs.GetRun(thread.Id, run.Id);

                    if (run.Status == RunStatus.RequiresAction &&
                        run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
                    {
                        var toolApprovals = new List<ToolApproval>();
                        foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                        {
                            if (toolCall is RequiredMcpToolCall mcpToolCall)
                            {
                                _logger.LogInformation("Approving MCP tool call: {Name}", mcpToolCall.Name);
                                toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                            }
                        }

                        if (toolApprovals.Count > 0)
                        {
                            run = _persistentAgentsClient.Runs.SubmitToolOutputsToRun(thread.Id, run.Id, toolApprovals: toolApprovals);
                        }
                    }
                }

                return Ok(new SendMessageResult
                {
                    ThreadId = threadId,
                    AgentId = agent.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<Microsoft.Agents.AI.AIAgent> FindAgentByNameAsync(string agentName)
        {
            var agents = _persistentAgentsClient.Administration.GetAgentsAsync();
            await foreach (var existingAgent in agents)
            {
                if (existingAgent.Name == agentName)
                {
                    return await _persistentAgentsClient.GetAIAgentAsync(existingAgent.Id);
                }
            }

            // If not found, create a new one
            var mcpServerUrl = _appConfig.ContosoStoreMcpUrl;
            var mcpServerLabel = _appConfig.ContosoStoreMcpServerLabel;
            var deploymentName = _appConfig.AzureOpenAIDeploymentName;

            MCPToolDefinition mcpTool = new(mcpServerLabel, mcpServerUrl);

            return await _persistentAgentsClient.CreateAIAgentAsync(
                model: deploymentName,
                name: agentName,
                //instructions: _agentConfig.GetSystemMessage(),
                tools: [mcpTool]);
        }
    }
}
