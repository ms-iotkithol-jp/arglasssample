using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace UWPIoTAIApp
{
    public class SupportJobInfo
    {
        public string JobId { get; set; }
        public string Title { get; set; }
        // Requesting|InResponse|Done
        public string Status { get; set; }
        public string Location { get; set; }
        public string Target { get; set; }
    }

    public class JobOrder
    {
        public delegate Task ShowLogDelegate(string content);
        public SupportJobInfo currentJobInfo { get; private set; }


        public JobOrder(DeviceClient deviceClient, MediaCapture mediaCapture, ShowLogDelegate showLog)
        {
            this.deviceClient = deviceClient;
            this.mediaCapture = mediaCapture;
            this.showLog = showLog;
        }

        private DeviceClient deviceClient;
        private MediaCapture mediaCapture;
        private ShowLogDelegate showLog;

        public async Task CheckCurrentJobStatusInReportedProperties()
        {
            if (currentJobInfo == null)
            {
                currentJobInfo = new SupportJobInfo();
            }
            var twin = await deviceClient.GetTwinAsync();
            var  reported = Newtonsoft.Json.JsonConvert.DeserializeObject(twin.Properties.Reported.ToJson()) as JObject;
            var jobInfo = reported["job"];
            if (jobInfo != null)
            {
                string jobid = jobInfo["jobid"].Value<string>();
                string jobstatus = jobInfo["status"].Value<string>();
                currentJobInfo.JobId = jobid;
                currentJobInfo.Status = jobstatus;
            }

            var desiredProps = twin.Properties.Desired.ToJson();
            await ResolveJobInfoInDesiredProps(desiredProps);
        }

        public async Task ResolveJobInfoInDesiredProps(string dp)
        {
            var dpJson = Newtonsoft.Json.JsonConvert.DeserializeObject(dp) as JObject;
            if (dpJson.ContainsKey("job-request"))
            {
                var supportJobRequest = dpJson["job-request"];
                if (supportJobRequest.Children().Count() > 0)
                {
                    var jobId = supportJobRequest["jobid"].Value<string>();
                    var title = supportJobRequest["title"].Value<string>();
                    var status = supportJobRequest["status"].Value<string>();
                    var location = supportJobRequest["location"].Value<string>();
                    var target = supportJobRequest["target"].Value<string>();
                    currentJobInfo.Title = title;
                    currentJobInfo.Location = location;
                    currentJobInfo.Target = target;
                    if (currentJobInfo.JobId != jobId)
                    {
                        if (currentJobInfo.Status == "Done")
                        {
                            currentJobInfo.JobId = jobId;
                            currentJobInfo.Status = status;
                        }
                    }
                }
            }
        }



        public async Task UpdateJobStatus(string status, Geolocator geolocator)
        {
            if (currentJobInfo != null)
            {
                var currentJobStatus = currentJobInfo.Status.ToLower();
                if (status == CommonConstants.VoiceCommand_JobStart)
                {
                    currentJobInfo.Status = "InResponse";
                }
                else if (status == CommonConstants.VoiceCommand_JobStop)
                {
                    currentJobInfo.Status = "Done";
                }
                await UpdateJobStatus(geolocator);
                showLog($"Updated job status -> {currentJobInfo.Status}");
            }
        }

        public async Task TryCheckTarget()
        {
            var orderToTarget = new
            {
                target = currentJobInfo.Target,
                order = "check",
                timestamp = DateTime.Now,
                jobid = currentJobInfo.JobId
            };
            var content = Newtonsoft.Json.JsonConvert.SerializeObject(orderToTarget);
            var msg = new Message(System.Text.Encoding.UTF8.GetBytes(content));
            msg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_MESSAGE_TYPE, CommonConstants.IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_TARGET);
            msg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_APP, CommonConstants.IOTHUB_MSG_PROPERTY_APP_VALUE);
            await deviceClient.SendEventAsync(msg);

        }

        private async Task UpdateJobStatus(Geolocator geolocator)
        {
            var reportedProperteis = new TwinCollection();
            var jobStatusProperty = new TwinCollection();
            jobStatusProperty["jobid"] = currentJobInfo.JobId;
            jobStatusProperty["status"] = currentJobInfo.Status;
            reportedProperteis["job"] = jobStatusProperty;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperteis);
            showLog("Update Job status:" + currentJobInfo.Status);

            var positoin = await geolocator.GetGeopositionAsync();

            var content = new
            {
                jobid = currentJobInfo.JobId,
                status = currentJobInfo.Status,
                timestamp = DateTime.Now,
                location = positoin
            };
            var msgJson = Newtonsoft.Json.JsonConvert.SerializeObject(content);
            var msg = new Message(System.Text.Encoding.UTF8.GetBytes(msgJson));
            msg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_APP, CommonConstants.IOTHUB_MSG_PROPERTY_APP_VALUE);
            msg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_MESSAGE_TYPE, CommonConstants.IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_JOB);
            await deviceClient.SendEventAsync(msg);
        }

        public async Task TakePictureAndUploadToBlob()
        {
            var photoFileName = "photo-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg";
            var myPictures = await Windows.Storage.StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
            StorageFile file = await myPictures.SaveFolder.CreateFileAsync(photoFileName, CreationCollisionOption.GenerateUniqueName);

            using (var captureStream = new InMemoryRandomAccessStream())
            {
                await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), captureStream);

                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var decoder = await BitmapDecoder.CreateAsync(captureStream);
                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(fileStream, decoder);

                    var properties = new BitmapPropertySet {
                        { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) }
                    };
                    await encoder.BitmapProperties.SetPropertiesAsync(properties);

                    await encoder.FlushAsync();
                }
            }
            var uploadRequest = new FileUploadSasUriRequest() { BlobName = photoFileName };
            var uploadSasUri = await deviceClient.GetFileUploadSasUriAsync(uploadRequest);
            var cloudBlockBlob = new CloudBlockBlob(uploadSasUri.GetBlobUri());
            using (var photoFileStream = await file.OpenStreamForReadAsync())
            {
                await cloudBlockBlob.UploadFromStreamAsync(photoFileStream);
            }
            showLog("Uploaded - " + uploadSasUri.GetBlobUri());
        }


    }
}
