using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Web.Http;

namespace WpfAppJobTracking
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string iothubcs = "<- IoT Hub Connection String for service role ->";
        private static readonly string baseURL = "<- SignalR Hub hosting Web site url ->";

        
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            serviceClient = ServiceClient.CreateFromConnectionString(iothubcs);
            await serviceClient.OpenAsync();
            registryManager = RegistryManager.CreateFromConnectionString(iothubcs);
            await registryManager.OpenAsync();

            try
            {
                await LoadDynaedgeDevices();
                await SetupSignalRClient();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task SetupSignalRClient()
        {
            var httpClient = new System.Net.Http.HttpClient();
            var response = await httpClient.PostAsync(baseURL + "/api/SignalRInfo", new System.Net.Http.StringContent(""));
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var responseContent = (await response.Content.ReadAsStringAsync());
                dynamic signalRInfoJson = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);
                string signalRUrl = signalRInfoJson["url"];
                string accessToken = signalRInfoJson["accessToken"];
                string hub = signalRInfoJson["hub"];
                var hubConnection = new HubConnectionBuilder().WithUrl(signalRUrl, (info) =>
                {
                    info.AccessTokenProvider = () => Task.FromResult(accessToken);
                }).Build();
                hubConnection.On<string>("SendData", async (msg) =>
                {
                    await this.Dispatcher.InvokeAsync(() =>
                  {
                      dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(msg);
                      dynamic message = json["message"];
                      dynamic position = message["position"];
                      double positionLatitude = position["Latitude"];
                      double positionLongitude = position["Longitude"];
                      double positionAltitude = position["Altitude"];
                      double positionAccuracy = position["Accuracy"];
                      double positionSpeed = position["Speed"];
                      ; string messageType = message["msgtype"];
                      if (messageType == "job")
                      {
                          string jobId = message["jobid"];
                          string jobStatus = message["status"];
                          string notifyTimestamp = message["timestamp"];
                          UpdateJobStatus(jobId, jobStatus).Wait();
                      }
                      ;
                  });
                });
                await hubConnection.StartAsync();
            }
        }

        private async Task AssginTagsToDevice(string deviceId, string parsonincharge)
        {
            var twin = await registryManager.GetTwinAsync(deviceId);
            var patch ="{ tags: { application: 'dynaedge', personincharge: '"+ parsonincharge+"' } }";
            await registryManager.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag);

        }

        private List<string> parsonInCharges = new List<string>();
        private Dictionary<string, string> deviceMaps = new Dictionary<string, string>();

        private async Task LoadDynaedgeDevices()
        {
            var query = registryManager.CreateQuery(
                "SELECT * FROM devices WHERE tags.application = 'dynaedge'");
            while (query.HasMoreResults)
            {
                var devices = await query.GetNextAsTwinAsync();
                foreach (var device in devices)
                {
                    string personInCharge = device.Tags["personincharge"];
                    parsonInCharges.Add(personInCharge);
                    deviceMaps.Add(personInCharge, device.DeviceId);
                }
            }
            lvWorkers.ItemsSource = parsonInCharges;
        }

        ServiceClient serviceClient;
        RegistryManager registryManager;

        private async void buttonCreateJob_Click(object sender, RoutedEventArgs e)
        {
            tbJobId.Text = Guid.NewGuid().ToString();
            tbJobStatus.Text = "requesting";
            var jobrequesting = new
            {
                jobid = tbJobId.Text,
                title = tbTitle.Text,
                target = tbTarget.Text,
                location = tbLocation.Text,
                status = tbJobStatus.Text
            };
            var desiredJson = Newtonsoft.Json.JsonConvert.SerializeObject(jobrequesting);
            var twin = await registryManager.GetTwinAsync(tbDeviceId.Text);
            
            var patch = "{ properties: { desired: { 'job-request': " + desiredJson + "} } }";
            await registryManager.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag);
            buttonRefreshJob.IsEnabled = true;
        }

        private async void buttonReflesh_Click(object sender, RoutedEventArgs e)
        {
            await UpdateDeviceReportedProps(selectedDeviceId);
        }

        private async void buttonSendCommand_Click(object sender, RoutedEventArgs e)
        {
            var msg = new Message(System.Text.Encoding.UTF8.GetBytes(tbOrder.Text));
            msg.Properties.Add("command", "support-order");
            await serviceClient.SendAsync(selectedDeviceId, msg);
        }

        private string selectedDeviceId;

        private async void lvWorkers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var worker = lvWorkers.SelectedItem.ToString();
            selectedDeviceId = deviceMaps[worker];
            tbDeviceId.Text = selectedDeviceId;
            await UpdateDeviceReportedProps(selectedDeviceId);
            buttonCreateJob.IsEnabled = true;
            buttonReflesh.IsEnabled = true;
            buttonSendCommand.IsEnabled = true;

            tbTitle.Text = "Maintenance and tuning";
            tbTarget.Text = "Electric instrument";
            tbLocation.Text = "Lake Yamanaka";
        }

        private async Task UpdateDeviceReportedProps(string deviceId)
        {
            var query = registryManager.CreateQuery(
                $"SELECT * FROM devices WHERE deviceId = '{deviceId}'");
            var devices = await query.GetNextAsTwinAsync();
            var device = devices.First();
            dynamic reportedPosition = device.Properties.Reported["location"]["position"];
            tbLatitude.Text = reportedPosition["Latitude"];
            tbLogitude.Text = reportedPosition["Longitude"];

            bool arGrassMounted = device.Properties.Reported["argrass"]["mounted"];
            if (arGrassMounted)
            {
                tbMount.Text = @"装着中";
            }
            else
            {
                tbMount.Text = @"未装着";
            }
        }

        private async void buttonRefreshJob_Click(object sender, RoutedEventArgs e)
        {
            var query = registryManager.CreateQuery(
                $"SELECT * FROM devices WHERE deviceId = '{tbDeviceId.Text}'");
            var devices = await query.GetNextAsTwinAsync();
            var device = devices.First();
            dynamic reportedJob = device.Properties.Reported["job"];
            string jobStatus = reportedJob["status"];
            string jobid = reportedJob["jobid"];
            await UpdateJobStatus(jobid, jobStatus);
        }

        private async Task UpdateJobStatus(string jobId, string jobStatus)
        {
            if (tbJobId.Text == jobId)
            {
                tbJobStatus.Text = jobStatus;
            
                if (jobStatus == "Done")
                {
                    var twin = await registryManager.GetTwinAsync(tbDeviceId.Text);

                    var patch = "{ properties: { desired: { 'job-request': null } } }";
                    await registryManager.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag);


                }
            }
        }
    }
}
