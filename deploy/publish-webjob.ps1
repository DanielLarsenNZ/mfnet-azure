$ErrorActionPreference = 'Stop'

. ./vars.ps1

# Package and zip the WebJob
dotnet publish ..\AudioFileCopy.sln --configuration Release -o './_zip/app_data/Jobs/Triggered/AudioFileCopy.WebJob'
copy ./run.cmd './_zip/app_data/Jobs/Triggered/AudioFileCopy.WebJob'
Compress-Archive -Path ./_zip/* -DestinationPath ./deploy.zip -Force

# Deploy source code
az webapp deployment source config-zip -g $rg -n $webjobApp --src ./deploy.zip
