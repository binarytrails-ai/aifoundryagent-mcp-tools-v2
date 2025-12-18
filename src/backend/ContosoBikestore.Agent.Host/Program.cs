using Azure.AI.Agents.Persistent;
using Azure.AI.OpenAI;
using Azure.Identity;
using ContosoBikestore.Agent.Host;
using ContosoBikestore.Agent.Host.Agents;
using ContosoBikestore.Agent.Host.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

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