using 'main.bicep'

param baseName = 'duragent'

param location = 'eastus2'

param resourceGroupName = 'rg-duragent'

param functionRuntimeName = 'dotnet-isolated'

param functionRuntimeVersion = '10.0'

param maximumInstanceCount = 100

param instanceMemoryMB = 2048

param tags = {
  project: 'durable-agent-demo'
  environment: 'dev'
}

param aiFoundryLocation = 'eastus2'

param modelName = 'gpt-5.1'

param modelFormat = 'OpenAI'

param modelVersion = '2025-11-13'

param modelSkuName = 'GlobalStandard'

param modelCapacity = 1
