using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.Devices;
using Microsoft.Extensions.Configuration;

namespace EGeorge
{
    public static class DetectObjectsToDynaedge
    {
        static ComputerVisionClient client;
        static ServiceClient serviceClient;

        [FunctionName("DetectObjectsToDynaedge")]
        public static async void Run([BlobTrigger("uploadedfiles/{name}", Connection = "trigger_STORAGE")]Stream myBlob, string name, ILogger log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json",optional:true, reloadOnChange: true).AddEnvironmentVariables().Build();
            var subscriptionKey = config.GetConnectionString("ComputerVisionKey");
            var endpoint = config.GetConnectionString("ComputerVisionEndpoint");
            var iothubcs = config.GetConnectionString("IoTHubConnectionString");

            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            if (client==null) {
                client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(subscriptionKey)) { Endpoint = endpoint };
            }
            if (serviceClient == null) {
                serviceClient = ServiceClient.CreateFromConnectionString(iothubcs);
                serviceClient.OpenAsync().Wait();
            }
            
            var detected = await client.DetectObjectsInStreamAsync(myBlob);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(detected);
            var devandname = name.Split("/");
            if (devandname.Length==2) {
                var deviceId = devandname[0];
                var msg = new Message(System.Text.Encoding.UTF8.GetBytes(json));
                msg.Properties.Add("command","detected-objects");
                await serviceClient.SendAsync(deviceId, msg);
//                var directMethod = new CloudToDeviceMethod("NotifyObjectDetection");
//                directMethod.SetPayloadJson(json);
//                var invocationResult = await serviceClient.InvokeDeviceMethodAsync(deviceId,directMethod);
//                log.LogInformation($"Invoked - {invocationResult.Status}");
                log.LogInformation($"detected objects - {detected.Objects.Count}");
                foreach (var dobj in detected.Objects) {
                    log.LogInformation($"detected - {dobj.ObjectProperty}");

                    if (dobj.ObjectProperty.Contains("instrument") || dobj.ObjectProperty.Contains("guitar")) {
                        var supportorder = @"弦交換、ネック調整";
                        msg = new Message(System.Text.Encoding.UTF8.GetBytes(supportorder));
                        msg.Properties.Add("command","support-order");
                        await serviceClient.SendAsync(deviceId, msg);
                        break;
                    }

                }
            }

        }
    }
}
