using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AudioFileCopy.Functions
{
    public static class SendMessage
    {
        [FunctionName(nameof(SendMessage))]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            [ServiceBus("audiofilecopy", Connection = "ServiceBusConnectionString")] IAsyncCollector<string> messages,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            await messages.AddAsync("SampleAudio_0.4mb.mp3,SampleAudio_0.4mb_Copy.mp3");

            return new OkObjectResult("Message sent OK");
        }
    }
}
