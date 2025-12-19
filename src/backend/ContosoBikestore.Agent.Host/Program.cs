using Azure.AI.Agents.Persistent;
using Azure.AI.OpenAI;
using Azure.Identity;
using ContosoBikestore.Agent.Host;
using ContosoBikestore.Agent.Host.Agents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddHttpClient();

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

// Configure OpenTelemetry
const string serviceName = "ContosoBikestore.Agent.Host";
const string serviceVersion = "1.0.0";

// Configure OpenTelemetry for Aspire dashboard
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4318";

// Create a resource to identify this service
var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
    .AddAttributes(new Dictionary<string, object>
    {
        ["service.instance.id"] = Environment.MachineName,
        ["deployment.environment"] = builder.Environment.EnvironmentName
    });

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new Dictionary<string, object>
        {
            ["service.instance.id"] = Environment.MachineName,
            ["deployment.environment"] = builder.Environment.EnvironmentName
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource(serviceName) // Our custom activity source
        .AddSource("*Microsoft.Agents.AI")
        .AddSource("Microsoft.Extensions.AI")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation() // .NET runtime metrics
        .AddMeter(serviceName) // Our custom meter
        .AddMeter("*Microsoft.Agents.AI")
        .AddMeter("Microsoft.Extensions.AI")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri(otlpEndpoint);
        }));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(resourceBuilder);
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri(otlpEndpoint);
    });
});

// Set up the Azure OpenAI client with OpenTelemetry instrumentation
IChatClient chatClient = new AzureOpenAIClient(new Uri(openAiEndpoint), new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry(sourceName: serviceName, configure: cfg => cfg.EnableSensitiveData = true) // Enable telemetry with sensitive data
    .Build();

//chatClient = new DebugChatClient(chatClient);

builder.Services.AddChatClient(chatClient);
builder.Services.AddAGUI();
builder.AddDevUI();

// Add OpenAI services
builder.AddOpenAIChatCompletions();
builder.AddOpenAIResponses();
builder.AddOpenAIConversations();

var jsonOptions = new Microsoft.AspNetCore.Http.Json.JsonOptions();

// Create specialized agents with OpenTelemetry instrumentation
var productInventoryAgent = (await ProductInventoryAgent.CreateAsync(chatClient, appConfig))
    .AsBuilder()
    .UseOpenTelemetry(serviceName, configure: cfg => cfg.EnableSensitiveData = true)
    .Build();

var billingAgent = (await BillingAgent.CreateAsync(chatClient, appConfig, jsonOptions))
    .AsBuilder()
    .UseOpenTelemetry(serviceName, configure: cfg => cfg.EnableSensitiveData = true)
    .Build();

var triageAgent = TriageAgent.Create(chatClient)
    .AsBuilder()
    .UseOpenTelemetry(serviceName, configure: cfg => cfg.EnableSensitiveData = true)
    .Build();

// Create handoff workflow where triage agent routes to specialists
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [productInventoryAgent, billingAgent])
    .WithHandoff(productInventoryAgent, triageAgent)
    .WithHandoff(billingAgent, triageAgent)
    .Build();

var workflowAgent = workflow.AsAgent(id: "customer-support-workflow", name: "CustomerSupportAgent");

// Log agent creation
var startupLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("Agents created successfully:");

builder.Services.AddKeyedSingleton<AIAgent>("ProductInventoryAgent", productInventoryAgent);
builder.Services.AddKeyedSingleton<AIAgent>("BillingAgent", billingAgent);
builder.Services.AddKeyedSingleton<AIAgent>("TriageAgent", triageAgent);
builder.Services.AddKeyedSingleton<AIAgent>("CustomerSupportAgent", workflowAgent);
builder.Services.AddKeyedSingleton<Workflow>("CustomerSupportWorkflow", workflow);

var app = builder.Build();

// Get logger for startup logging
var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("=== Contoso Bikestore Agent Host Starting ===");
logger.LogInformation("Service: {ServiceName}, Version: {ServiceVersion}", serviceName, serviceVersion);
logger.LogInformation("OTLP Endpoint: {OtlpEndpoint}", otlpEndpoint);
logger.LogInformation("OpenAI Endpoint: {OpenAIEndpoint}", openAiEndpoint);
logger.LogInformation("Deployment: {DeploymentName}", deploymentName);

app.MapOpenApi();
app.UseCors();

app.MapOpenAIResponses();
app.MapOpenAIConversations();

app.MapOpenAIChatCompletions(workflowAgent);
app.MapOpenAIChatCompletions(productInventoryAgent);
app.MapOpenAIChatCompletions(billingAgent);

// Map AGUI endpoint - only expose the workflow agent to users
app.MapAGUI("/agent/contoso_assistant", workflowAgent);

// Map DevUI - it will discover and use all registered agents including the workflow agent
app.MapDevUI();

logger.LogInformation("Application configured successfully. Available endpoints:");
logger.LogInformation("  - AGUI: /agent/contoso_assistant");
logger.LogInformation("  - DevUI: /devui");
logger.LogInformation("  - OpenAPI: /openapi/v1.json");
logger.LogInformation("Application started. Listening for requests...");

await app.RunAsync();