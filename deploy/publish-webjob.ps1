$ErrorActionPreference = 'Stop'

. ./vars.ps1

# Package and zip the WebJob
rd ./_zip -Recurse -Force
dotnet publish ..\AudioFileCopy.sln --configuration Release -o './_zip/app_data/Jobs/Continuous/AudioFileCopy.WebJob' -f net461 --no-self-contained
copy ./run.cmd './_zip/app_data/Jobs/Continuous/AudioFileCopy.WebJob'
Compress-Archive -Path ./_zip/* -DestinationPath ./deploy.zip -Force

# Deploy source code
az webapp deployment source config-zip -g $rg -n $webjobApp --src ./deploy.zip
