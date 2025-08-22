

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Name of storage account.')
param storageAccountName string = 'storage${uniqueString(resourceGroup().id)}'

var storageAccountType = 'Standard_LRS'


resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageAccountType
  }
  kind: 'StorageV2'
}


output staname string = storageAccount.id
