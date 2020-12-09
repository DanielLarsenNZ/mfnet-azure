using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace AudioFileCopy.WebJob
{
    public class Functions
    {
        static readonly string OutputPath = string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("WEBROOT_PATH"))
                        ? Directory.GetCurrentDirectory()
                        : Environment.GetEnvironmentVariable("WEBROOT_PATH");

        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        public static void ProcessQueueMessage(
            [ServiceBusTrigger("audiofilecopy", Connection = "ServiceBusConnectionString")] string message,
            [Blob("audio/{outputFile.Name}", FileAccess.Write, Connection = "DataStorageConnectionString")] FileStream outputFile,
            ILogger log)
        {
            try
            {
                log.LogInformation(message);

                var args = message.Split(',');

                if (args.Length != 2) throw new Exception("sourceFile, outputFile");

                var fileInfo = new FileInfo(args[1]);
                string filename = fileInfo.Name;

                string outputFilepath = $"{OutputPath}\\{filename}";
                log.LogInformation($"outputFilepath = {outputFilepath}");

                using (var audioFileCopy = new AudioFileCopier())
                {
                    audioFileCopy.CopyFile(args[0], outputFilepath);
                }

                outputFile = new FileStream(outputFilepath, FileMode.Open);
            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
