// Module to deploy App Service Plan, Backend App Service, and Frontend App Service
param resourcePrefix string
param uniqueSuffixValue string
param location string
param tags object
param foundryProjectEndpoint string
param foundryProjectName string
param openAIDeploymentName string
param openAIEndpoint string
param appInsightsConnectionString string = ''

var frontendAppName = '${resourcePrefix}-web-${uniqueSuffixValue}'
var backendAppName = '${resourcePrefix}-api-${uniqueSuffixValue}'
var contosoStoreAppName = '${resourcePrefix}-contoso-store-${uniqueSuffixValue}'
var mcpServerAppName = '${resourcePrefix}-mcp-${uniqueSuffixValue}'

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${resourcePrefix}-plan-${uniqueSuffixValue}'
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  tags: tags
}

// Linux App Service Plan for Node.js frontend
resource linuxAppServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${resourcePrefix}-plan-linux-${uniqueSuffixValue}'
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
  tags: tags
}

// Backend App Service
resource backendApp 'Microsoft.Web/sites@2022-03-01' = {
  name: backendAppName
  location: location
  tags: union(tags, {
    'azd-service-name': 'api'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      appSettings: [
        {
          name: 'FRONTEND_APP_URL'
          value: 'https://${frontendAppName}.azurewebsites.net'
        }
        {
          name: 'AZURE_AI_PROJECT_ENDPOINT'
          value: foundryProjectEndpoint
        }
        {
          name: 'AZURE_AI_PROJECT_NAME'
          value: foundryProjectName
        }
        {
          name: 'AZURE_OPENAI_DEPLOYMENT_NAME'
          value: openAIDeploymentName
        }
        {
          name: 'AZURE_OPENAI_ENDPOINT'
          value: openAIEndpoint
        }
        {
          name: 'Azure__TenantId'
          value: subscription().tenantId
        }
        {
          name: 'Azure__SubscriptionId'
          value: subscription().subscriptionId
        }
        {
          name: 'CONTOSO_STORE_MCP_URL'
          value: 'https://${mcpServerApp.name}.azurewebsites.net/sse'
        }
        {
          name: 'CONTOSO_STORE_MCP_SERVER_API_KEY'
          value: 'b7f3e2c1-4a5d-4e8b-9c2a-7f6d1e3a2b4c'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ]
    }
  }
}

// Frontend App Service (Linux)
resource frontendApp 'Microsoft.Web/sites@2022-03-01' = {
  name: frontendAppName
  location: location
  kind: 'app,linux'
  tags: union(tags, {
    'azd-service-name': 'web'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: linuxAppServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|20-lts'
      appCommandLine: 'node server.js'
      appSettings: [
        {
          name: 'BACKEND_AGENT_BASE_URL'
          value: 'https://${backendAppName}.azurewebsites.net'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'NODE_ENV'
          value: 'production'
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'WEBSITE_NODE_DEFAULT_VERSION'
          value: '20-lts'
        }
        {
          name: 'NEXT_TELEMETRY_DISABLED'
          value: '1'
        }
      ]
    }
  }
}

resource contosoStoreApp 'Microsoft.Web/sites@2022-03-01' = {
  name: contosoStoreAppName
  location: location
  tags: union(tags, {
    'azd-service-name': 'contoso-store-api'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ]
    }
  }
}

resource mcpServerApp 'Microsoft.Web/sites@2022-03-01' = {
  name: mcpServerAppName
  location: location
  tags: union(tags, {
    'azd-service-name': 'contoso-store-mcp'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      appSettings: [
        {
          name: 'CONTOSO_STORE_URL'
          value: 'https://${contosoStoreAppName}.azurewebsites.net'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'API_KEY'
          value: 'b7f3e2c1-4a5d-4e8b-9c2a-7f6d1e3a2b4c'
        }
      ]
    }
  }
}

// Role assignment for backend app system-assigned managed identity
resource backendAppRoleAssignment1 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(backendApp.id, 'backend-role-azureai-developer')
  scope: resourceGroup()
  properties: {
    principalType: 'ServicePrincipal'
    principalId: backendApp.identity.principalId
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', '64702f94-c441-49e6-a78b-ef80e0188fee')
  }
}

resource backendAppRoleAssignment2 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(backendApp.id, 'backend-role-cognitive-services-user')
  scope: resourceGroup()
  properties: {
    principalType: 'ServicePrincipal'
    principalId: backendApp.identity.principalId
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
  }
}

output BACKEND_APP_URL string = 'https://${backendApp.name}.azurewebsites.net'
output FRONTEND_APP_URL string = 'https://${frontendApp.name}.azurewebsites.net'
output CONTOSO_STORE_APP_URL string = 'https://${contosoStoreApp.name}.azurewebsites.net'
output CONTOSO_STORE_MCP_URL string = 'https://${mcpServerApp.name}.azurewebsites.net/sse'
