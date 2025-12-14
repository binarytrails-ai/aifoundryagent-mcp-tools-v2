# Using MCP Tools with AI Foundry Agents

This repository is an AI assistant solution built for Contoso Bike Store that demonstrates the use of MCP (Model Context Protocol) tools with Azure AI Foundry Agents. 

The application provides a chat interface where customers can interact with the Contoso Bike Store AI agent:

![Chat Interface - Welcome](resources/chat-1.png)

![Chat Interface - Product Inquiry](resources/chat-2.png)

## Architecture Overview

![Architecture Overview](resources/architecture.png)

- **Frontend**: React application hosted on Azure App Service providing a chat interface
- **Backend**: ASP.NET Core Web API (Chat Orchestrator API) hosted on Azure App Service that processes user prompts
- **Azure AI Foundry**: Hosts the Contoso Agent with access to MCP tools and models (GPT-4.1)
- **MCP Server**: Custom MCP server that extends agent capabilities with domain-specific tools
- **Product Microservice**: REST API providing product inventory and order management data

### MCP Tool Categories

1. **Product Inventory Tools**:
   - Get bike models and specifications
   - Check accessory availability
   - Query inventory levels

2. **Order Management Tools**:
   - Create new orders
   - Check order status
   - Track order history

## Prerequisites

1. **Azure Subscription**: Sign up for a [free Azure account](https://azure.microsoft.com/free/) if you don't have one
2. **GitHub Account**: Required for repository access and authentication
3. **Development Environment**:
   - Visual Studio Code with Dev Container support, or
   - GitHub Codespaces (recommended)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/rakeshl4/aifoundryagent_mcp_tools.git
cd aifoundryagent_mcp_tools
```

### 2. Set Up Development Environment

#### Option A: GitHub Codespaces (Recommended)

Click the button below to launch the project in GitHub Codespaces:

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://github.com/codespaces/new?hide_repo_select=true&ref=main&repo=YOUR_REPO_ID&skip_quickstart=true)

#### Option B: Local Development with VS Code Dev Container

1. Open the project in Visual Studio Code
2. Install the Dev Containers extension
3. Press `Ctrl+Shift+P` and select "Dev Containers: Reopen in Container"

### 3. Deploy Azure Resources

1. **Authenticate with Azure**:

   ```powershell
   azd auth login --use-device-code
   ```

2. **Create and configure environment**:

   ```powershell
   azd env new dev
   azd env select dev
   azd env set AZURE_LOCATION australiaeast
   ```

3. **Deploy infrastructure**:

   ```powershell
   azd up
   ```

4. **Copy environment variables**:

   ```powershell
   cp .azure/dev/.env .env
   ```

## Project Structure

```text
├── infra/                   # Infrastructure as Code (Bicep files)
├── resources/              # API specifications and sample data
├── scripts/                # Utility scripts
├── src/
│   ├── backend/            # AI Agent API (.NET)
│   └── frontend/           # Web application
└── azure.yaml              # Azure Developer CLI configuration
```

## Resources

- [Azure AI Foundry Documentation](https://learn.microsoft.com/azure/ai-foundry/)
- [Azure AI Foundry Agent Service](https://learn.microsoft.com/en-us/azure/ai-services/agents/overview)
- [Model Context Protocol](https://modelcontextprotocol.io/)