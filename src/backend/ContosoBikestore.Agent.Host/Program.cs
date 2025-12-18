using Azure.AI.Agents.Persistent;
using Azure.AI.OpenAI;
using Azure.Identity;
using ContosoBikestore.Agent.Host;
using ContosoBikestore.Agent.Host.Agents;
using ContosoBikestore.Agent.Host.Models;
using ContosoBikestore.Agent.Host.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddHttpClient().AddLogging();

builder.Services.AddSingleton<AppConfig>(sp => new AppConfig(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Set up configuration
var appConfig = new AppConfig(builder.Configuration);
var projectEndpoint = appConfig.AzureAIAgentProjectEndpoint;
var deploymentName = appConfig.AzureOpenAIDeploymentName;
var openAiEndpoint = appConfig.AzureOpenAiServiceEndpoint;

// Set up the Azure OpenAI client
IChatClient chatClient = new AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();

chatClient = new DebugChatClient(chatClient);

builder.Services.AddChatClient(chatClient);
builder.Services.AddAGUI();
builder.AddDevUI();

// Add OpenAI services
builder.AddOpenAIChatCompletions();
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var jsonOptions = new Microsoft.AspNetCore.Http.Json.JsonOptions();

// Create specialized agents
var productInventoryAgent = await ProductInventoryAgent.CreateAsync(chatClient, appConfig);
var billingAgent = await BillingAgent.CreateAsync(chatClient, appConfig, jsonOptions);
var triageAgent = TriageAgent.Create(chatClient);

// Create handoff workflow where triage agent routes to specialists
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [productInventoryAgent, billingAgent])
    .WithHandoff(productInventoryAgent, triageAgent)
    .WithHandoff(billingAgent, triageAgent)
    .Build();

var workflowAgent = workflow.AsAgent(id: "customer-support-workflow", name: "CustomerSupportAgent");

// Wrap workflow agent with approval middleware for AG-UI

//var workflowAgent = workflowAgentBase
//    .AsBuilder()
//    //.Use(runFunc: null, runStreamingFunc: (messages, thread, options, innerAgent, cancellationToken) =>
//    //    HandleApprovalRequestsMiddleware(
//    //        messages,
//    //        thread,
//    //        options,
//    //        innerAgent,
//    //        jsonOptions.SerializerOptions,
//    //        cancellationToken))
//    .Build();

builder.Services.AddKeyedSingleton<AIAgent>("ProductInventoryAgent", productInventoryAgent);
builder.Services.AddKeyedSingleton<AIAgent>("BillingAgent", billingAgent);
builder.Services.AddKeyedSingleton<AIAgent>("TriageAgent", triageAgent);
builder.Services.AddKeyedSingleton<AIAgent>("CustomerSupportAgent", workflowAgent);
builder.Services.AddKeyedSingleton<Workflow>("CustomerSupportWorkflow", workflow);

var app = builder.Build();
app.MapOpenApi();
app.UseCors();

app.MapOpenAIResponses();
app.MapOpenAIConversations();

// Map endpoints for workflow agent (this provides seamless multi-agent experience)
app.MapOpenAIChatCompletions(workflowAgent);

// Map AGUI endpoint - only expose the workflow agent to users
app.MapAGUI("/agent/contoso_assistant", workflowAgent);

// Map DevUI - it will discover and use all registered agents including the workflow agent
app.MapDevUI();
await app.RunAsync();

//// AG-UI Approval Middleware - Converts between Microsoft.Extensions.AI approval types and AG-UI protocol
//static async IAsyncEnumerable<AgentRunResponseUpdate> HandleApprovalRequestsMiddleware(
//    IEnumerable<ChatMessage> messages,
//    AgentThread? thread,
//    AgentRunOptions? options,
//    AIAgent innerAgent,
//    JsonSerializerOptions jsonSerializerOptions,
//    [EnumeratorCancellation] CancellationToken cancellationToken)
//{
//    // Process messages: Convert approval responses back to FunctionApprovalResponseContent
//    var modifiedMessages = ConvertApprovalResponsesToFunctionApprovals(messages, jsonSerializerOptions);

//    // Invoke inner agent
//    Console.WriteLine("[MIDDLEWARE] Starting inner agent streaming...");
//    var updateCount = 0;
//    await foreach (var update in innerAgent.RunStreamingAsync(
//        modifiedMessages, thread, options, cancellationToken))
//    {
//        updateCount++;
//        Console.WriteLine($"[MIDDLEWARE] Received update #{updateCount} - Role: {update.Role}, Contents: {update.Contents.Count}");

//        // Process updates: Convert FunctionApprovalRequestContent to client tool calls
//        await foreach (var processedUpdate in ConvertFunctionApprovalsToToolCalls(update, jsonSerializerOptions))
//        {
//            yield return processedUpdate;
//        }
//    }
//    Console.WriteLine($"[MIDDLEWARE] Inner agent stream completed. Total updates: {updateCount}");

//    // WORKAROUND: If the last update had no content, emit a final completion marker
//    // This ensures AG-UI receives a proper terminal event
//    // TODO: Investigate why workflow doesn't emit final message after handoff
//    yield return new AgentRunResponseUpdate(ChatRole.Assistant, []);

//    // Local function: Convert approval responses from client back to FunctionApprovalResponseContent
//    static IEnumerable<ChatMessage> ConvertApprovalResponsesToFunctionApprovals(
//        IEnumerable<ChatMessage> messages,
//        JsonSerializerOptions jsonSerializerOptions)
//    {
//        // Look for "request_approval" tool calls and their matching results
//        Dictionary<string, FunctionCallContent> approvalToolCalls = [];
//        FunctionResultContent? approvalResult = null;

//        foreach (var message in messages)
//        {
//            foreach (var content in message.Contents)
//            {
//                if (content is FunctionCallContent { Name: "request_approval" } toolCall)
//                {
//                    approvalToolCalls[toolCall.CallId] = toolCall;
//                }
//                else if (content is FunctionResultContent result && approvalToolCalls.ContainsKey(result.CallId))
//                {
//                    approvalResult = result;
//                }
//            }
//        }

//        // If no approval response found, return messages unchanged
//        if (approvalResult == null)
//        {
//            return messages;
//        }

//        // Deserialize the approval response
//        if ((approvalResult.Result as JsonElement?)?.Deserialize(jsonSerializerOptions.GetTypeInfo(typeof(ApprovalResponse))) is not ApprovalResponse response)
//        {
//            return messages;
//        }

//        // Extract the original function call details from the approval request
//        var originalToolCall = approvalToolCalls[approvalResult.CallId];

//        if (originalToolCall.Arguments?.TryGetValue("request", out object? requestObj) != true ||
//            requestObj is not JsonElement request ||
//            request.Deserialize(jsonSerializerOptions.GetTypeInfo(typeof(ApprovalRequest))) is not ApprovalRequest approvalRequest)
//        {
//            return messages;
//        }

//        var originalFunctionCall = new FunctionCallContent(
//            callId: response.ApprovalId,
//            name: approvalRequest.FunctionName,
//            arguments: approvalRequest.FunctionArguments);

//#pragma warning disable MEAI001
//        var functionApprovalResponse = new FunctionApprovalResponseContent(
//            response.ApprovalId,
//            response.Approved,
//            originalFunctionCall);
//#pragma warning restore MEAI001

//        // Replace/remove the approval-related messages
//        List<ChatMessage> newMessages = [];
//        foreach (var message in messages)
//        {
//            bool hasApprovalResult = false;
//            bool hasApprovalRequest = false;

//            foreach (var content in message.Contents)
//            {
//                if (content is FunctionResultContent { CallId: var callId } && callId == approvalResult.CallId)
//                {
//                    hasApprovalResult = true;
//                    break;
//                }
//                if (content is FunctionCallContent { Name: "request_approval", CallId: var reqCallId } && reqCallId == approvalResult.CallId)
//                {
//                    hasApprovalRequest = true;
//                    break;
//                }
//            }

//            if (hasApprovalResult)
//            {
//                // Replace tool result with approval response
//                newMessages.Add(new ChatMessage(ChatRole.User, [functionApprovalResponse]));
//            }
//            else if (hasApprovalRequest)
//            {
//                // Skip the request_approval tool call message
//                continue;
//            }
//            else
//            {
//                newMessages.Add(message);
//            }
//        }

//        return newMessages;
//    }

//    // Local function: Convert FunctionApprovalRequestContent to client tool calls
//    static async IAsyncEnumerable<AgentRunResponseUpdate> ConvertFunctionApprovalsToToolCalls(
//        AgentRunResponseUpdate update,
//        JsonSerializerOptions jsonSerializerOptions)
//    {
//        // Check if this update contains a FunctionApprovalRequestContent
//#pragma warning disable MEAI001
//        FunctionApprovalRequestContent? approvalRequestContent = null;
//        foreach (var content in update.Contents)
//        {
//            if (content is FunctionApprovalRequestContent request)
//            {
//                approvalRequestContent = request;
//                break;
//            }
//        }
//#pragma warning restore MEAI001

//        // If no approval request, yield the update unchanged
//        if (approvalRequestContent == null)
//        {
//            yield return update;
//            yield break;
//        }

//        // Convert the approval request to a "client tool call"
//        var functionCall = approvalRequestContent.FunctionCall;
//        var approvalId = approvalRequestContent.Id;

//        var approvalData = new ApprovalRequest
//        {
//            ApprovalId = approvalId,
//            FunctionName = functionCall.Name,
//            FunctionArguments = functionCall.Arguments,
//            Message = $"Approve execution of '{functionCall.Name}'?"
//        };

//        var approvalJson = JsonSerializer.Serialize(approvalData, jsonSerializerOptions.GetTypeInfo(typeof(ApprovalRequest)));

//        // Yield a tool call update that represents the approval request
//        yield return new AgentRunResponseUpdate(ChatRole.Assistant, [
//            new FunctionCallContent(
//                callId: approvalId,
//                name: "request_approval"
//                //arguments: new Dictionary<string, object?> { ["request"] = approvalJson }
//                )
//        ]);

//        yield return update;
//    }
//}
