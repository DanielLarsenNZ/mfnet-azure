# Deploy App Service Plan

. ./vars.ps1

# RESOURCE GROUP
Write-Host "az group create -n $rg" -ForegroundColor Yellow
az group create -n $rg --location $location --tags $tags


# STORAGE ACCOUNTS
az storage account create -n $webjobsStorage -g $rg -l $location --tags $tags --sku Standard_LRS
az storage account create -n $dataStorage -g $rg -l $location --tags $tags --sku Standard_LRS
$webjobsStorageConnection = ( az storage account show-connection-string -g $rg -n $webjobsStorage | ConvertFrom-Json ).connectionString
$dataStorageConnection = ( az storage account show-connection-string -g $rg -n $dataStorage | ConvertFrom-Json ).connectionString


# APPLICATION INSIGHTS
az extension add -n application-insights
$instrumentationKey = ( az monitor app-insights component create --app $insights --location $location -g $rg --tags $tags | ConvertFrom-Json ).instrumentationKey


# APP SERVICE PLAN
Write-Host "az appservice plan create -n $plan" -ForegroundColor Yellow
az appservice plan create -n $plan -g $rg --location $location --sku $sku --number-of-workers $numberWorkers --tags $tags

# Create WebJob app
az webapp create -n $webjobApp --plan $plan -g $rg --tags $tags
#Write-Host "az webapp deployment source config -n $webjobApp" -ForegroundColor Yellow
#az webapp deployment source config -n $webjobApp -g $rg --repo-url 'https://github.com/DanielLarsenNZ/mfnet-azure'

# Disable ARR
Write-Host "az webapp update -n $webjobApp --client-affinity-enabled false" -ForegroundColor Yellow
az webapp update -n $webjobApp -g $rg --client-affinity-enabled false


# APP SETTINGS
az webapp config appsettings set -n $webjobApp -g $rg --settings `
    "APPINSIGHTS_INSTRUMENTATIONKEY=$instrumentationKey" `
    "AzureWebJobsStorage=$webjobsStorageConnection" `
    "AzureWebJobsDashboard=$webjobsStorageConnection" `
    "DataStorageConnectionString=$dataStorageConnection"
