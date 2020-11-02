using Azure.Storage.Blobs;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.SpeechRecognition;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Navigation;
using Microsoft.WindowsAzure.Storage.Blob;
using Windows.ApplicationModel.ExtendedExecution;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x411 を参照してください

namespace UWPIoTAIApp
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.KeyDown += MainPage_KeyDown;
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // F21,?,F23
            ShowLog("Key Down:" + e.Key);
            if (e.Key== Windows.System.VirtualKey.F21)
            {
                await TrySpeechAndAction();
            }
            if (e.Key== Windows.System.VirtualKey.F23)
            {
                await KickIoTHubSending();
            }
        }

        private async void buttonSpeech_Click(object sender, RoutedEventArgs e)
        {
            await TrySpeechAndAction();
        }

        private async void buttonIoT_Click(object sender, RoutedEventArgs e)
        {
            await KickIoTHubSending();
        }


        private string iotDeviceName = "";
        private string iothubCS = "<- IoT Hub Device Connection String ->";

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TryEnterFullScreenMode();

            await InitializeCamera();
            await SetupIoTHub();
            await SetupLocationTracking();
            await SetupSensors();

            jobOrder = new JobOrder(deviceClient, mediaCapture, ShowLog);
            await jobOrder.CheckCurrentJobStatusInReportedProperties();
            ShowJobInfo(jobOrder.currentJobInfo);

            buttonSpeech.Focus(FocusState.Keyboard);
            buttonIoT.Focus(FocusState.Keyboard);
            myWebView.LoadCompleted += MyWebView_LoadCompleted;
        }

        private async Task ShowWebPage(string orderJson)
        {
            var json = Newtonsoft.Json.JsonConvert.DeserializeObject(orderJson) as JObject;
            var descrip = (json["description"] as JValue).Value<string>();
            var url = (json["url"] as JValue).Value<string>();
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    myWebView.Navigate(new Uri(url));
                });
        }

        private void MyWebView_LoadCompleted(object sender, NavigationEventArgs e)
        {
            ShowLog($"Web View Loaded : {e.Content}");
            myWebViewScrollViewer.ZoomMode = ZoomMode.Enabled;
         //   myWebViewScrollViewer.ChangeView(zoomFactor: 0.2f, horizontalOffset: null, verticalOffset: null);
         //   var tfg = new TransformGroup();
         //   tfg.Children.Add(new ScaleTransform() { CenterX = 0, CenterY = 0, ScaleX = 0.5, ScaleY = 0.5 });
         //   myWebView.RenderTransform = tfg;
        }


        MediaCapture mediaCapture;
        JobOrder jobOrder;
        bool cameraPreviwing = false;

        private async Task InitializeCamera()
        {
            if (mediaCapture == null)
            {
                var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (allVideoDevices.Count() > 0)
                {
                    var cameraDevice = allVideoDevices.FirstOrDefault();
                    mediaCapture = new MediaCapture();
                    var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };
                    mediaCapture.CameraStreamStateChanged += MediaCapture_CameraStreamStateChanged;
                    mediaCapture.RecordLimitationExceeded += MediaCapture_RecordLimitationExceeded;
                    try
                    {
                        await mediaCapture.InitializeAsync(settings);
                        myCanvas.Source = mediaCapture;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Debug.WriteLine("The app was denied access to the camera");
                    }
                }

            }
        }

        private async Task ControlCameraCapture(bool start)
        {
            if (start)
            {
                cameraPreviwing = true;
                await mediaCapture.StartPreviewAsync();
                mediaCapture.ThermalStatusChanged += MediaCapture_ThermalStatusChanged;
                ShowLog("Video Preview Started");
            }
            else
            {
                cameraPreviwing = false;
                await mediaCapture.StopPreviewAsync();
                mediaCapture.ThermalStatusChanged -= MediaCapture_ThermalStatusChanged;
                ShowLog("Video Preview Stoped");
            }
        }

        private void MediaCapture_ThermalStatusChanged(MediaCapture sender, object args)
        {
            ShowLog($"Thermal Status Changed - {sender.ThermalStatus}");
        }

        private void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            ShowLog("Record Limitation Exceeded");
        }

        private async void MediaCapture_CameraStreamStateChanged(MediaCapture sender, object args)
        {
            ShowLog($"Camera Stream State Changed - {sender.CameraStreamState}");
            if (sender.CameraStreamState == Windows.Media.Devices.CameraStreamState.NotStreaming)
            {
                if (cameraPreviwing)
                {
                    await sender.StartPreviewAsync();
                    ShowLog("Retry to start preview");
                }
            }
        }

        private async Task TrySpeechAndAction()
        {
            var language = new Windows.Globalization.Language("ja-JP");
            var slangs = SpeechRecognizer.SupportedTopicLanguages;
            foreach (var slang in slangs)
            {
                var name = slang.DisplayName;
            }
            var langs = SpeechRecognizer.SupportedGrammarLanguages;
            foreach (var lang in langs)
            {
                var name = lang.DisplayName;
            }
            // Create an instance of SpeechRecognizer.
            var speechRecognizer = new SpeechRecognizer(language);

            // Listen for audio input issues.
            speechRecognizer.RecognitionQualityDegrading += SpeechRecognizer_RecognitionQualityDegrading;

            // Add a web search grammar to the recognizer.
            var webSearchGrammar = new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.WebSearch, "webSearch");


            speechRecognizer.UIOptions.AudiblePrompt = "Say what you want to search for...";
            speechRecognizer.UIOptions.ExampleText = @"Ex. '開始','撮影','確認','完了'";
            int commandKeyId = (int)CommandKeyword.VoiceCommand;
            lock (targetedOrders)
            {
                if (targetedOrders.ContainsKey(commandKeyword[commandKeyId]))
                {
                    speechRecognizer.UIOptions.ExampleText = @"Ex." + targetedOrders[commandKeyword[commandKeyId]];
                }
            }
            //speechRecognizer.Constraints.Add(webSearchGrammar);

            // Compile the constraint.
            await speechRecognizer.CompileConstraintsAsync();

            // Start recognition.
            var speechRecognitionResult = await speechRecognizer.RecognizeWithUIAsync();
            //await speechRecognizer.RecognizeWithUIAsync();

            // Do something with the recognition result.
            // var messageDialog = new Windows.UI.Popups.MessageDialog(speechRecognitionResult.Text, "Text spoken");

            ShowLog($"Recognized {speechRecognitionResult.Text}");
            if (speechRecognitionResult.Text == CommonConstants.VoiceCommand_JobStart)
            {
                await jobOrder.UpdateJobStatus(speechRecognitionResult.Text, geolocator);
            }
            else if (speechRecognitionResult.Text == CommonConstants.VoiceCommand_JobStop)
            {
                await jobOrder.UpdateJobStatus(speechRecognitionResult.Text, geolocator);
            }
            else if (speechRecognitionResult.Text == CommonConstants.VoiceCommand_PreviewCamera)
            {
                if (mediaCapture != null)
                {
                    if (mediaCapture.CameraStreamState == Windows.Media.Devices.CameraStreamState.NotStreaming)
                    {
                        await ControlCameraCapture(true);
                        //   mediaCapture.StartPreviewAsync();
                    }
                    else
                    {
                        await ControlCameraCapture(false);
                        //                        mediaCapture.StopPreviewAsync();
                    }

                }
            }
            else if (speechRecognitionResult.Text == CommonConstants.VoiceCommand_TakeAndUploadPicture)
            {
                if (mediaCapture != null && cameraPreviwing)
                {
                    await jobOrder.TakePictureAndUploadToBlob();
                }
            }
            else if (speechRecognitionResult.Text == CommonConstants.VoiceCommand_CheckTarget)
            {
                await jobOrder.TryCheckTarget();
            }

            //            await messageDialog.ShowAsync();
        }
        private void SpeechRecognizer_RecognitionQualityDegrading(SpeechRecognizer sender, SpeechRecognitionQualityDegradingEventArgs args)
        {
            ShowLog(args.Problem.ToString());
        }


        private DeviceClient deviceClient;
        CancellationTokenSource iotHubSendingTokenSource;
        CancellationTokenSource iotHubReceivingTokenSource;

        private async Task SetupIoTHub()
        {
            var csUnits = iothubCS.Split(";");
            iotDeviceName = csUnits[1].Split("=")[1];
            deviceClient = DeviceClient.CreateFromConnectionString(iothubCS);
            //    await deviceClient.OpenAsync();
            await MarkDeviceAsDynaedgeApp();

            deviceClient.SetConnectionStatusChangesHandler(statusCangesHandle);
            await deviceClient.SetMethodDefaultHandlerAsync(methodHandler, this);
            await deviceClient.SetDesiredPropertyUpdateCallbackAsync(desiredPropertyHandler, this);


            iotHubReceivingTokenSource = new CancellationTokenSource();
            ReceiveIoTHubMessages(iotHubReceivingTokenSource.Token);
        }

        private async Task MarkDeviceAsDynaedgeApp()
        {
            var reportedProperteis = new TwinCollection();
            var appProps = new TwinCollection();
            appProps["name"] = "dynaedge-support";
            appProps["version"] = "0.1.0";
            reportedProperteis["application"] = appProps;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperteis);
        }

        private void statusCangesHandle(ConnectionStatus status, ConnectionStatusChangeReason reason)
        {
            switch (status)
            {
                case ConnectionStatus.Disconnected:
                    ShowLog("IoT Hub -> disconnected. Try to Connect.");
                    if (reason != ConnectionStatusChangeReason.No_Network)
                    {
                        deviceClient = DeviceClient.CreateFromConnectionString(iothubCS);
                    }
                    else
                    {
                        ShowLog("IoT Hub disconnected reason = " + reason.ToString());
                    }
                    break;
                case ConnectionStatus.Connected:
                    ShowLog("IoT Hub -> connected.");
                    break;
                case ConnectionStatus.Disconnected_Retrying:
                    ShowLog("IoT Hub -> Disconnected retrying by " + reason.ToString());
                    break;
                case ConnectionStatus.Disabled:
                    ShowLog("IoT Hub -> Disabled by " + reason.ToString());
                    break;
            }
        }

        private async Task desiredPropertyHandler(TwinCollection desiredProperties, object userContext)
        {
            var dp = desiredProperties.ToJson();
            await jobOrder.ResolveJobInfoInDesiredProps(dp);
            ShowJobInfo(jobOrder.currentJobInfo);
        }

        private async Task ShowJobInfo(SupportJobInfo jobInfo=null)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (jobInfo != null)
                {
                    tbJobTitle.Text = jobInfo.Title;
                    tbJobTarget.Text = jobInfo.Target;
                    tbJobLocation.Text = jobInfo.Location;
                }
                else
                {
                    tbJobTitle.Text = "";
                    tbJobTarget.Text = "";
                    tbJobLocation.Text = "";
                }
            });
        }

        private async Task<MethodResponse> methodHandler(MethodRequest methodRequest, object userContext)
        {
            int responseCode = 200;
            ShowLog(string.Format("Invoked - MethodName:{0},Payload:[1]", methodRequest.Name, methodRequest.DataAsJson));
            string payload = "{\"message\":\"got it!\"}";
            switch (methodRequest.Name)
            {
                case "NotifyObjectDetection":
                    await MarkDetectedObjects(methodRequest.DataAsJson);
                    break;
                case "ShowWebPage":
                    await ShowWebPage(methodRequest.DataAsJson);
                    break;
                default:
                    break;
            }
            var response = new MethodResponse(System.Text.Encoding.UTF8.GetBytes(payload), responseCode);
            return response;
        }

        private async Task MarkDetectedObjects(string dataJson)
        {
            var detecteds = ParseDetections(dataJson);
            foreach (var detected in detecteds.Objects)
            {
                await DrawRecangle(detected.rectangle, string.Format("{0}:{1:0.###%}", detected.detected, detected.confidence), 5);
            }
        }

        private  ObjectsInPhotos ParseDetections(string objectdetect)
        {
            var result = new ObjectsInPhotos();

            var djson = Newtonsoft.Json.JsonConvert.DeserializeObject(objectdetect) as JObject;
            var metadata = djson["metadata"];
            if (!(metadata is null) && metadata is JObject)
            {
                var jometadata = metadata as JObject;
                result.PhotoWidth = (jometadata["width"] as JValue).Value<double>();
                result.PhotoHeight = (jometadata["height"] as JValue).Value<double>();
                result.PhotoFormat = (jometadata["format"] as JValue).Value<string>();
            }
            var objects = djson["objects"];
            if (!(objects is null) && objects is JArray)
            {
                var objs = objects as JArray;
                for (int i = 0; i < objs.Count; i++)
                {
                    var obj = objs[i];
                    if (obj is JObject)
                    {
                        var jobj = obj as JObject;
                        var rectObj = jobj["rectangle"] as JObject;
                        var detected = (jobj["object"] as JValue).Value<string>();
                        var confidence = (jobj["confidence"] as JValue).Value<double>();
                        var rect = new Rect();
                        rect.X = (rectObj["x"] as JValue).Value<double>();
                        rect.Y = (rectObj["y"] as JValue).Value<double>();
                        rect.Width = (rectObj["w"] as JValue).Value<double>();
                        rect.Height = (rectObj["h"] as JValue).Value<double>();
                        rect.X /= result.PhotoWidth;
                        rect.Y /= result.PhotoHeight;
                        rect.Width /= result.PhotoWidth;
                        rect.Height /= result.PhotoHeight;
                        var detectedRect = new ObjectDetected()
                        {
                            rectangle = rect,
                            detected = detected,
                            confidence = confidence
                        };
                        result.Objects.Add(detectedRect);
                    }
                }
            }
            return result;
        }

        class ObjectDetected
        {
            public Rect rectangle { get; set; }
            public string detected { get; set; }
            public double confidence { get; set; }
        }
        class ObjectsInPhotos
        {
            private List<ObjectDetected> detected = new List<ObjectDetected>();
            public List<ObjectDetected> Objects { get { return detected; } }
            public double PhotoWidth { get; set; }
            public double PhotoHeight { get; set; }
            public string PhotoFormat { get; set; }
        }


        class TextMarkedRectangle
        {
            public Rectangle Rectangle { get; set; }
            public TextBlock Text { get; set; }
        }
        List<TextMarkedRectangle> currentRectangles = new List<TextMarkedRectangle>();
        private async Task DrawRecangle(Rect rectShape,string descrip, int duration)
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,()=>{
                var rectangle = new Rectangle();
                rectangle.Stroke = new SolidColorBrush(Windows.UI.Colors.Red);
                rectangle.StrokeThickness = 2;
                var canvasW = ovCanvas.ActualWidth;
                var canvasH = ovCanvas.ActualHeight;
                var lty = canvasH * rectShape.Top;
                var ltx = canvasW * rectShape.Left;
                rectangle.Width = canvasW * rectShape.Width;
                rectangle.Height = canvasH * rectShape.Height;
                ovCanvas.Children.Add(rectangle);
                Canvas.SetLeft(rectangle,ltx);
                Canvas.SetTop(rectangle, lty);
                var text = new TextBlock();
                text.Text = descrip;
                text.FontSize = 10;
                text.Foreground = new SolidColorBrush(Windows.UI.Colors.Red);
                ovCanvas.Children.Add(text);
                Canvas.SetLeft(text, ltx);
                Canvas.SetTop(text, lty+rectangle.Height+text.ActualHeight);
                var currentMarkedRectangle = new TextMarkedRectangle() { Rectangle = rectangle, Text = text };
                lock (currentRectangles)
                {
                    currentRectangles.Add(currentMarkedRectangle);
                }
                var dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Interval = TimeSpan.FromSeconds(duration);
                dispatcherTimer.Tick += ((s,e)=> {
                    ovCanvas.Children.Remove(rectangle);
                    ovCanvas.Children.Remove(text);
                    lock (currentRectangles)
                    {
                        currentRectangles.Remove(currentMarkedRectangle);
                    }
                    dispatcherTimer.Stop();
                });
                dispatcherTimer.Start();
                
            });
        }

        Dictionary<string, string> targetedOrders = new Dictionary<string, string>();
        private enum CommandKeyword
        {
            VoiceCommand = 0,
            SupportOrder = 1,
            ShowWeb = 2,
            DetectedObjects = 3
        }
        string[] commandKeyword = { "voice-command", "support-order", "show-web", "detected-objects" };
        string commandKey = "command";

        private async Task ReceiveIoTHubMessages(CancellationToken cs)
        {
            cs.ThrowIfCancellationRequested();
            while (true)
            {
                var msg = await deviceClient.ReceiveAsync(cs);
                if (msg != null)
                {
                    var msgStr = System.Text.Encoding.UTF8.GetString(msg.GetBytes());
                    lock (targetedOrders)
                    {
                        if (msg.Properties.ContainsKey(commandKey))
                        {
                            var iothubcommand = msg.Properties[commandKey];
                            if (targetedOrders.ContainsKey(iothubcommand))
                            {
                                targetedOrders[iothubcommand] = msgStr;
                            }
                            else
                            {
                                targetedOrders.Add(iothubcommand, msgStr);
                            }
                        }
                    }
                    if (targetedOrders.ContainsKey(commandKeyword[(int)CommandKeyword.ShowWeb])){
                        await ShowWebPage(targetedOrders[commandKeyword[(int)CommandKeyword.ShowWeb]]);
                    }
                    if (targetedOrders.ContainsKey(commandKeyword[(int)CommandKeyword.DetectedObjects]))
                    {
                        ShowLog($"Receive object detection - {targetedOrders[commandKeyword[(int)CommandKeyword.DetectedObjects]]}");
                        await MarkDetectedObjects(targetedOrders[commandKeyword[(int)CommandKeyword.DetectedObjects]]);
                    }
                    if (targetedOrders.ContainsKey(commandKeyword[(int)CommandKeyword.SupportOrder]))
                    {
                        await ShowSupportOrder(targetedOrders[commandKeyword[(int)CommandKeyword.SupportOrder]]);
                    }

                    await deviceClient.CompleteAsync(msg);
                    ShowLog("Received - " + msgStr);
                }
                if (cs.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ShowSupportOrder(string order) {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                tbOrder.Text = order;
            });
        }

        ExtendedExecutionSession extendedSession;
        Geolocator geolocator;
        private async Task SetupLocationTracking()
        {
            var now = DateTime.Now;
            var accessStatus = await Geolocator.RequestAccessAsync();
            switch (accessStatus)
            {
                case GeolocationAccessStatus.Allowed:
                    ShowLog("Geolocation Access Status -> Allowed");
                    await UpdateGeolocationStatus("Geolocation Access Status -> Allowed", now);
                    // If DesiredAccuracy or DesiredAccuracyInMeters are not set (or value is 0), DesiredAccuracy.Default is used.
                    geolocator = new Geolocator { DesiredAccuracyInMeters = 100 };

                    // Subscribe to the StatusChanged event to get updates of location status changes.
                    geolocator.StatusChanged += Geolocator_StatusChanged;
                    geolocator.PositionChanged += Geolocator_PositionChanged;

                    // Carry out the operation.
                    Geoposition pos = await geolocator.GetGeopositionAsync();

                    await UpdateLocationData(pos, now);
                    break;

                case GeolocationAccessStatus.Denied:
                    ShowLog("Access to location is denied.");
                    await UpdateGeolocationStatus("GeolocationAccessStatus.Denied", now);
                    break;

                case GeolocationAccessStatus.Unspecified:
                    ShowLog("Access to location -> Unspecified error.");
                    await UpdateGeolocationStatus("GeolocationAccessStatus.Unspecified", now);
                    break;
                default:
                    ShowLog("Access to location -> Unknown.");
                    await UpdateGeolocationStatus("GeolocationAccessStatus-Unknown", now);
                    break;
            }

            if (accessStatus == GeolocationAccessStatus.Allowed)
            {
                extendedSession = new ExtendedExecutionSession();
                extendedSession.Reason = ExtendedExecutionReason.LocationTracking;
                extendedSession.Description = "Update location status";
                extendedSession.Revoked += ExtendedSession_Revoked;
                var extendedSessionResult = await extendedSession.RequestExtensionAsync();
                if (extendedSessionResult == ExtendedExecutionResult.Allowed)
                {
                    ShowLog("Location updating will be run even if this app goes to background.");
                }
                else
                {
                    ShowLog("Location updating will be run only when this app is forground");
                }
            }
        }

        private void ExtendedSession_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            var now = DateTime.Now;
            switch (args.Reason)
            {
                case ExtendedExecutionRevokedReason.Resumed:
                    ShowLog("Extendded Session Revoked - Resumed");
                    break;
                case ExtendedExecutionRevokedReason.SystemPolicy:
                    ShowLog("Extendded Session Revoked - System Policy");
                    break;
            }
        }

        private async void Geolocator_PositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            var now = DateTime.Now;
            try
            {
                await UpdateLocationData(args.Position, now);
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
        }

        private async void Geolocator_StatusChanged(Geolocator sender, StatusChangedEventArgs args)
        {
            var now = DateTime.Now;
            switch (args.Status)
            {
                case PositionStatus.Disabled:
                    await UpdatePositionStatus("PositionStatus.Disabled", now);
                    break;
                case PositionStatus.Initializing:
                    await UpdatePositionStatus("PositionStatus.Initializing", now);
                    break;
                case PositionStatus.NoData:
                    await UpdatePositionStatus("PositionStatus.NoData", now);
                    break;
                case PositionStatus.NotAvailable:
                    await UpdatePositionStatus("PositionStatus.NotAvailable", now);
                    break;
                case PositionStatus.NotInitialized:
                    await UpdatePositionStatus("PositionStatus.NotInitialized", now);
                    break;
                case PositionStatus.Ready:
                    await UpdatePositionStatus("PositionStatus.Ready", now);
                    break;
            }
        }

        private async Task UpdateGeolocationStatus(string status, DateTime timestamp)
        {
            var reportedProperteis = new TwinCollection();
            var locationStatusProperty = new TwinCollection();
            locationStatusProperty["status"] = status;
            locationStatusProperty["timestamp"] = timestamp;
            reportedProperteis[CommonConstants.IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_LOCATION] = locationStatusProperty;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperteis);
            ShowLog(status);
        }

        private async Task UpdateGeolocationData(Geoposition position, DateTime timestamp)
        {
            var reportedProperteis = new TwinCollection();
            var locationStatusProperty = new TwinCollection();
            locationStatusProperty["position"] = position.Coordinate;
            locationStatusProperty["timestamp"] = timestamp;
            reportedProperteis[CommonConstants.IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_LOCATION] = locationStatusProperty;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperteis);
            ShowLog("UpdateGeoloationData");
        }
        private async Task UpdateLocationData(Geoposition pos, DateTime timestamp)
        {
            string json = "";
            try
            {
                CompassReading compassReading = null;
                if (compass != null)
                {
                    try
                    {
                        compassReading = compass.GetCurrentReading();
                    }
                    catch (Exception ex)
                    {
                        ShowLog(ex.Message);
                    }
                }
                await UpdateGeolocationData(pos, timestamp);
                var positionInfo = new
                {
                    timestamp = timestamp,
                    position = pos.Coordinate,
                    compass = compassReading
                };
                json = Newtonsoft.Json.JsonConvert.SerializeObject(positionInfo);
            }
            catch (Exception ex)
            {
                ShowLog(ex.Message);
            }
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var msg = new Message(System.Text.Encoding.UTF8.GetBytes(json));
                    msg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_MESSAGE_TYPE, CommonConstants.IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_LOCATION);
                    msg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_APP, CommonConstants.IOTHUB_MSG_PROPERTY_APP_VALUE);
                    await deviceClient.SendEventAsync(msg);
                    ShowLog("Send Location - " + json);
                }
                catch (Exception ex)
                {
                    ShowLog(ex.Message);
                }
            }
        }

        private async Task UpdatePositionStatus(string status, DateTime timestamp)
        {
            var reportedProperties = new TwinCollection();
            var locationStatusProperty = new TwinCollection();
            locationStatusProperty["position_status"] = status;
            reportedProperties[CommonConstants.IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_LOCATION] = locationStatusProperty;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            ShowLog(status);
        }
        bool toSend = false;



        private Dictionary<string, List<object>> measuredSensorReadings;

        private Accelerometer accelerometer;
        private Gyrometer gyrometer;
        private ProximitySensor proximity;
        private LightSensor light;
        private Compass compass;


        private async Task SendSensorValues(CancellationToken cs)
        {
            cs.ThrowIfCancellationRequested();
            while (true)
            {
                foreach (var sensortype in measuredSensorReadings.Keys)
                {
                    var msgs = new List<object>();
                    lock (measuredSensorReadings)
                    {
                        msgs.AddRange(measuredSensorReadings[sensortype]);
                        measuredSensorReadings[sensortype].Clear();
                    }
                    if (msgs.Count() > 0)
                    {
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(msgs);
                        var jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
                        var iotMsg = new Message(jsonBytes);
                        iotMsg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_MESSAGE_TYPE, CommonConstants.IOTHUB_MSG_PROPERTY_MESSAGE_TYPE_SENSOR);
                        iotMsg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_SENSOR_TYPE, sensortype);
                        iotMsg.Properties.Add(CommonConstants.IOTHUB_MSG_PROPERTY_KEY_APP, CommonConstants.IOTHUB_MSG_PROPERTY_APP_VALUE);
                        await deviceClient.SendEventAsync(iotMsg);
                        if (jsonBytes.Length > 256000)
                        {
                            Debug.WriteLine("Too long message! - {0}", jsonBytes.Length);
                        }
                    }
                }
                await Task.Delay(100);
                if (cs.IsCancellationRequested)
                {
                    break;
                }
            }
//            await deviceClient.CloseAsync();
        }

        private bool currentProximity = false;
        private SensingConfig sensingConfig = new SensingConfig();

        private async Task SetupSensors()
        {
            measuredSensorReadings = new Dictionary<string, List<object>>();
            measuredSensorReadings.Add("accelerometer", new List<object>());
            measuredSensorReadings.Add("gyrometer", new List<object>());
            measuredSensorReadings.Add("proximity", new List<object>());
            measuredSensorReadings.Add("light", new List<object>());
            measuredSensorReadings.Add("compass", new List<object>());
            try
            {
                if (accelerometer == null)
                {
                    accelerometer = Accelerometer.GetDefault();
                    if (accelerometer != null)
                    {
                        sensingConfig.Add(accelerometer, Accelerometer_ReadingChanged);
                    }
                }
                if (gyrometer == null)
                {
                    gyrometer = Gyrometer.GetDefault();
                    if (gyrometer != null)
                    {
                        sensingConfig.Add(gyrometer, Gyrometer_ReadingChanged);
                    }
                }
                if (light == null)
                {
                    light = LightSensor.GetDefault();
                    if (light != null)
                    {
                        sensingConfig.Add(light, Light_ReadingChanged);
                    }
                }
                if (compass == null)
                {
                    compass = Compass.GetDefault();

                    if (compass != null)
                    {
                        sensingConfig.Add(compass, Compass_ReadingChanged);
                    }
                }
                if (proximity == null)
                {
                    var selector = ProximitySensor.GetDeviceSelector();
                    var myDevices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(selector);
                    if (myDevices.Count > 0)
                    {
                        proximity = ProximitySensor.FromId(myDevices[0].Id);
                        if (proximity != null)
                        {
                            sensingConfig.Add(proximity, Proximity_ReadingChanged);
                            var reading = proximity.GetCurrentReading();
                            if (reading != null)
                            {
                                await UpdateMountStatus(reading.IsDetected);
                                currentProximity = reading.IsDetected;
                            }
                            else
                            {
                                await UpdateMountStatus(false);
                                currentProximity = false;
                            }

                        }
                    }
                }
                //sensingConfig.Set(accelerometer: true, gyrometer: true, compass: true, proximity: true, light: true);
                sensingConfig.Set(accelerometer: false, gyrometer: false, compass: false, proximity: false, light: false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private async Task UpdateMountStatus(bool isDetected)
        {
            var reportedProperteis = new TwinCollection();
            var argrassStatusProperty = new TwinCollection();
            argrassStatusProperty["mounted"] =isDetected;
            reportedProperteis["argrass"] = argrassStatusProperty;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperteis);
            ShowLog("Update mounted status:"+isDetected);
        }


        private async Task ShowLog(string content)
        {
            if (content.Contains("デバイスの"))
            {
                Debug.WriteLine($"Problem Happens - {content}");
            }
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var sb = new StringBuilder();
                sb.AppendLine(content);
                sb.Append(tbLog.Text);                
                tbLog.Text = sb.ToString();
            });

        }

        

        private async void Proximity_ReadingChanged(ProximitySensor sender, ProximitySensorReadingChangedEventArgs args)
        {
            var reading = new
            {
                sensortype = "proximity",
                timestamp = args.Reading.Timestamp,
                detected = args.Reading.IsDetected,
                distance = args.Reading.DistanceInMillimeters
            };
            lock (measuredSensorReadings)
            {
                if (toSend)
                {
                    measuredSensorReadings["proximity"].Add(reading);
                }
            }
            if (currentProximity != args.Reading.IsDetected)
            {
                await UpdateMountStatus(args.Reading.IsDetected);
                currentProximity = args.Reading.IsDetected;
            }
        }

        private void Compass_ReadingChanged(Compass sender, CompassReadingChangedEventArgs args)
        {
            var reading = new
            {
                sensortype = "compass",
                timestamp = args.Reading.Timestamp,
                heading_accuracy = args.Reading.HeadingAccuracy,
                heading_magnetic_north = args.Reading.HeadingMagneticNorth,
                meading_true_north = args.Reading.HeadingTrueNorth
            };
            lock (measuredSensorReadings)
            {
                if (toSend)
                {
                    measuredSensorReadings["compass"].Add(reading);
                }
            }
        }

        private void Light_ReadingChanged(LightSensor sender, LightSensorReadingChangedEventArgs args)
        {
            var reading = new
            {
                sensortype = "light",
                timestamp = args.Reading.Timestamp,
                illuminance = args.Reading.IlluminanceInLux
            };
            lock (measuredSensorReadings)
            {
                if (toSend)
                {
                    measuredSensorReadings["light"].Add(reading);
                }
            }
        }

        private void Gyrometer_ReadingChanged(Gyrometer sender, GyrometerReadingChangedEventArgs args)
        {
            var reading = new
            {
                sensortype = "gyrometer",
                timestamp = args.Reading.Timestamp,
                x = args.Reading.AngularVelocityX,
                y = args.Reading.AngularVelocityY,
                z = args.Reading.AngularVelocityZ
            };
            lock (measuredSensorReadings)
            {
                if (toSend)
                {
                    measuredSensorReadings["gyrometer"].Add(reading);
                }
            }
        }

        private void Accelerometer_ReadingChanged(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
        {
            var reading = new
            {
                sensortype = "accelerometer",
                timestamp = args.Reading.Timestamp,
                x = args.Reading.AccelerationX,
                y = args.Reading.AccelerationY,
                z = args.Reading.AccelerationZ
            };
            lock (measuredSensorReadings)
            {
                if (toSend)
                {
                    measuredSensorReadings["accelerometer"].Add(reading);
                }
            }
        }

        private async Task KickIoTHubSending()
        {
            if (buttonIoT.Content.ToString().StartsWith("Start"))
            {
                iotHubSendingTokenSource = new CancellationTokenSource();
                lock (measuredSensorReadings)
                {
                    foreach (var sensortype in measuredSensorReadings.Keys)
                    {
                        measuredSensorReadings[sensortype].Clear();
                    }
                    toSend = true;
                }
                sensingConfig.Set(true, true, true, true, true);
                SendSensorValues(iotHubSendingTokenSource.Token);
                buttonIoT.Content = "Stop";
            }
            else
            {
                lock (measuredSensorReadings)
                {
                    toSend = false;
                }
                buttonIoT.Content = "Start";
                sensingConfig.Set(false, false, false, false, false);
                iotHubSendingTokenSource.Cancel();
            }
        }
        delegate void Accelerometer_ReadingChangedHandler(Accelerometer sender, AccelerometerReadingChangedEventArgs args);
        delegate void Gyrometer_ReadingChangedHandler(Gyrometer sender, GyrometerReadingChangedEventArgs args);
        delegate void Compass_ReadingChangedHandler(Compass sender, CompassReadingChangedEventArgs args);
        delegate void LightSensor_ReadingChangedHandler(LightSensor sender, LightSensorReadingChangedEventArgs args);
        delegate void ProximitySensor_ReadingChangedHandler(ProximitySensor sender, ProximitySensor args);
        class SensingConfig
        {
            private bool _accelerometer;
            private bool _gyrometer;
            private bool _compass;
            private bool _proximity;
            private bool _light;

            public bool Accelerometer
            {
                get
                {
                    lock (this)
                    {
                        return _accelerometer;
                    }
                }
            }
            public bool Gyrometer
            {
                get
                {
                    lock (this)
                    {
                        return _gyrometer;
                    }
                }
            }
            public bool Compass
            {
                get
                {
                    lock (this)
                    {
                        return _compass;
                    };
                }
            }
            public bool Proximity
            {
                get
                {
                    lock (this)
                    {
                        return _proximity;
                    }
                }
            }
            public bool Light
            {
                get
                {
                    lock (this)
                    {
                        return _light;
                    }
                }
            }
            public SensingConfig()
            {
                _accelerometer = false;
                _gyrometer = false;
                _compass = false;
                _proximity = false;
                _light = false;
            }
            public void Set(bool accelerometer, bool gyrometer, bool compass, bool proximity, bool light)
            {
                lock (this)
                {
                    if (_accelerometer)
                    {
                        if (accelerometer == false)
                        {
                            accelerometerSensor.ReadingChanged -= accelerometerHandler;
                        }
                    }else
                    {
                        if (accelerometer)
                        {
                            accelerometerSensor.ReadingChanged += accelerometerHandler;
                        }
                    }
                    if (_gyrometer)
                    {
                        if (gyrometer == false)
                        {
                            gyrometerSensor.ReadingChanged -= gyrometerHandler;
                        }
                    }
                    else
                    {
                        if (gyrometer)
                        {
                            gyrometerSensor.ReadingChanged += gyrometerHandler;
                        }
                    }
                    if (_compass)
                    {
                        if (compass == false)
                        {
                            compassSensor.ReadingChanged -= compassHandler;
                        }
                    }
                    else
                    {
                        if (compass)
                        {
                            compassSensor.ReadingChanged += compassHandler;
                        }
                    }
                    if (_light)
                    {
                        if (light == false)
                        {
                            lightSensor.ReadingChanged -= lightSensorHandler;
                        }
                    }
                    else
                    {
                        if (light)
                        {
                            lightSensor.ReadingChanged += lightSensorHandler;
                        }
                    }
                    if (_proximity)
                    {
                        if (proximity == false)
                        {
                            proximitySensor.ReadingChanged -= proximitySensorHandler;
                        }
                    }
                    else
                    {
                        if (proximity)
                        {
                            proximitySensor.ReadingChanged += proximitySensorHandler;
                        }
                    }
                    _accelerometer = accelerometer;
                    _gyrometer = gyrometer;
                    _compass = compass;
                    _proximity = proximity;
                    _light = light;
                }
            }
            public void Add(Accelerometer sensor, TypedEventHandler<Accelerometer, AccelerometerReadingChangedEventArgs> handler)
            {
                accelerometerSensor = sensor;
                accelerometerHandler = handler;
            }
            public void Add(Gyrometer sensor, TypedEventHandler<Gyrometer, GyrometerReadingChangedEventArgs> handler)
            {
                gyrometerSensor = sensor;
                gyrometerHandler = handler;
            }
            public void Add(Compass sensor, TypedEventHandler<Compass, CompassReadingChangedEventArgs> handler)
            {
                compassSensor = sensor;
                compassHandler = handler;
            }
            public void Add(LightSensor sensor, TypedEventHandler<LightSensor, LightSensorReadingChangedEventArgs> handler)
            {
                lightSensor = sensor;
                lightSensorHandler = handler;
            }
            public void Add(ProximitySensor sensor, TypedEventHandler<ProximitySensor, ProximitySensorReadingChangedEventArgs> handler)
            {
                proximitySensor = sensor;
                proximitySensorHandler = handler;
            }

            private Accelerometer accelerometerSensor;
            private Gyrometer gyrometerSensor;
            private Compass compassSensor;
            private LightSensor lightSensor;
            private ProximitySensor proximitySensor;

            private TypedEventHandler<Accelerometer, AccelerometerReadingChangedEventArgs> accelerometerHandler;
            private TypedEventHandler<Gyrometer,GyrometerReadingChangedEventArgs> gyrometerHandler;
            private TypedEventHandler<Compass, CompassReadingChangedEventArgs> compassHandler;
            private TypedEventHandler<ProximitySensor,ProximitySensorReadingChangedEventArgs> proximitySensorHandler;
            private TypedEventHandler<LightSensor,LightSensorReadingChangedEventArgs> lightSensorHandler;
        }

    }
}
