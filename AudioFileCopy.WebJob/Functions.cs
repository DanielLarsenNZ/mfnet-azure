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
        public static void AudioFileCopy(
            [BlobTrigger("audio/{name}", Connection = "DataStorageConnectionString")] Stream inputFile,
            string name,
            Uri uri,
            [Blob("audio-output/{name}", FileAccess.Write, Connection = "DataStorageConnectionString")] Stream outputFile,
            ILogger log)
        {
            try
            {
                log.LogInformation($"Blob name = {name}, Uri = {uri}");

                string inputTempFilePath = Path.GetTempFileName();
                string outputTempFilePath = $"{Path.GetTempFileName()}.mp3";

                log.LogInformation($"inputTempFilePath = {inputTempFilePath}, outputTempFilePath = {outputTempFilePath}");

                using (var fileStream = File.Create(inputTempFilePath))
                {
                    inputFile.CopyTo(fileStream);
                }

                using (var audioFileCopy = new AudioFileCopier())
                {
                    audioFileCopy.CopyFile(inputTempFilePath, outputTempFilePath);
                }

                using (var outputTempFile = new FileStream(outputTempFilePath, FileMode.Open))
                    outputTempFile.CopyTo(outputFile);

                log.LogInformation($"Audio copied to {uri.ToString().Replace("/audio/", "/audio-output/")}");

            }
            catch (Exception ex)
            {
                log.LogError(ex, ex.Message);
                throw;
            }
        }
    }
}
