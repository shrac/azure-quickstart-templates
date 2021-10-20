$resourceGroup="appResourcesGroup"
$storageName="appstorageaccountshrac"

Compress-Archive -Path .\artifacts\* artifacts.zip -Force

# Sign in to your Azure subscription
Connect-AzAccount
# Create resource group for managed application definition and application package
#New-AzResourceGroup -Name $resourceGroup -Location eastus

# Create storage account for a package with application artifacts
#$storageAccount=New-AzStorageAccount `
#  -ResourceGroupName $resourceGroup `
#  -Name $storageName `
#  -SkuName Standard_LRS `
#  -Location eastus `

# Get the SA
$storageAccount=Get-AzStorageAccount `
  -ResourceGroupName $resourceGroup `
  -Name $storageName `

$ctx=$storageAccount.Context

# Create storage container and upload zip to blob
Get-AzStorageContainer -Name appcontainer -Context $ctx

Set-AzStorageBlobContent `
  -File "artifacts.zip" `
  -Container appcontainer `
  -Blob app.zip `
  -Context $ctx

# Get blob absolute uri
$StorageContext = New-AzureStorageContext -StorageAccountName $storageName
(Get-AzureStorageBlob -Container appcontainer -Blob app.zip -Context $StorageContext).ICloudBlob.uri.AbsoluteUri
