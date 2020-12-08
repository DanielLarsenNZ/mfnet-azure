using Microsoft.Azure.WebJobs;
using System;
using System.IO;

namespace AudioFileCopy.WebJob
{
    public class Functions
    {
        // This function will get triggered/executed when a new message is written 
        // on an Azure Queue called queue.
        public static void ProcessQueueMessage(
            [ServiceBusTrigger("audiofilecopy", Connection = "ServiceBusConnectionString")] string message, 
            TextWriter log)
        {
            log.WriteLine(message);

            var args = message.Split(',');

            if (args.Length != 2) throw new Exception("sourceFile, outputFile");

            using (var audioFileCopy = new AudioFileCopier())
            {
                audioFileCopy.CopyFile(args[0], args[1]);
            }

        }
    }
}
