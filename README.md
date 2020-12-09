# Windows Media Foundation .NET in Azure

Includes a fork of <https://github.com/OfItselfSo/Tanta> to demonstrate WMF processes running in Azure.

## Getting Started

You will need:

* Azure Subscription
* az CLI installed
* PowerShell installed
* .NET Framework 4.6.1 installed

To deploy Azure resources:

    cd deploy
    ./deploy-azure.ps1

To build and publish WebJob to Azure:

    ./publish-webjob.ps1

To test WebJob, upload an MP3 file to the **audio** container (create if not exists) in the Data Storage Account. Output will be created in the **audio-output** container with the same filename.

## References & links

<https://github.com/OfItselfSo/Tanta>

[Understanding runtimeconfig.json](https://www.programmersought.com/article/70243717433/)

[MediaFoundation nuget](https://www.nuget.org/packages/MediaFoundation/)

[How to create service bus trigger webjob?](https://stackoverflow.com/questions/58647763/how-to-create-service-bus-trigger-webjob)
