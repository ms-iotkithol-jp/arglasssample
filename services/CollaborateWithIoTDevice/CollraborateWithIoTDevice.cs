using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.EventHubs;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Devices;

namespace EGeorge
{
    public static class CollraborateWithIoTDevice
    {
        private static ServiceClient serviceClient;
        [FunctionName("CollraborateWithIoTDevice")]
        public static async Task Run([EventHubTrigger("dynaedgecommand", Connection = "egeh20200617_listen_EVENTHUB")] EventData[] events, ILogger log, ExecutionContext context)
        {
            if (serviceClient == null) {
                var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json",optional:true, reloadOnChange: true).AddEnvironmentVariables().Build();
                var iothubcs = config.GetConnectionString("IoTHubConnectionString");
                serviceClient = ServiceClient.CreateFromConnectionString(iothubcs);
                await serviceClient.OpenAsync(); 
            }
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    string messageBody = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
                    dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(messageBody);

                    if ( json.jobtarget != null && json.jobid != null && json.iotdevice != null && json.devicecommand != null && json.devicecommandarg != null ) {
                        string iotdevice = json.iotdevice;
                        string methodname = json.devicecommand;
                        string commandarg = json.devicecommandarg;
                        string jobid = json.jobid;
                        CloudToDeviceMethod directMethod = new CloudToDeviceMethod(methodname);
                        string payload = "{\"value\":\"" + commandarg + "\"}";
                        directMethod.SetPayloadJson(payload);
                        CloudToDeviceMethodResult invocationResult = null;
                        if ( iotdevice.IndexOf('/') > 0 ) {
                            var deviceIdAndMethodName = iotdevice.Split("/");
                            var deviceid = deviceIdAndMethodName[0];
                            var moduleName = deviceIdAndMethodName[1];
                            invocationResult = await serviceClient.InvokeDeviceMethodAsync(deviceid, moduleName, directMethod);
                        } else {
                            log.LogInformation($"Invoking[{jobid}] {iotdevice}.{methodname}({payload})");
                            invocationResult = await serviceClient.InvokeDeviceMethodAsync(iotdevice, directMethod);
                        }
                        if (invocationResult != null)
                            log.LogInformation($"Invocation[{jobid}] done. - {invocationResult.Status}");
                    }

                    // Replace these two lines with your processing logic.
                    log.LogInformation($"C# Event Hub trigger function processed a message: {messageBody}");
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
