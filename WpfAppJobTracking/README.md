# デバイスリスト表示と、デバイスへのジョブ要求、ジョブ状態変化トラッキング  
以下の機能を有する。  
- [UWPIoTAIApp](../UWPIoTAIApp)が稼働中の機器の一覧表示
- 一覧表示から機器を選択し、機器へのジョブリクエスト
- 選択した機器の位置表示、ジョブ状態表示  

## How To 
[MainWindow.xaml.cs](../MainWindow.xaml.cs) の 27、28行目の  
```c#
    public partial class MainWindow : Window
    {
        string iothubcs = "<- IoT Hub Connection String for service role ->";
        private static readonly string baseURL = "<- SignalR Hub hosting Web site url ->";
```
接続文字列を設定する。'iothubcs' には、UWPIoTApp が登録された IoT Hub のサービスロールの接続文字列を設定し、baseURL には、[https://github.com/ms-iotkithol-jp/IoTDataShareBySignalRService](https://github.com/ms-iotkithol-jp/IoTDataShareBySignalRService) で作成した、HubForSignalRService のURLを設定する。  

機器の一覧表示では、UWPIoTAIApp が登録されている Azure IoT Hub の各デバイスの Device Twins のタグに登録された担当者名が表示されるようになっている。なので、このアプリを実行する前に、Azure Portal で Azure IoT Hub の項目を開き、登録した全てのデバイスに対して、Device Twinsの項目を開き、properties の直前に、 
```json
  "tags": {
    "application": "dynaedge",
    "personincharge": "題名 栄治"
  },
  "properties": {
    "desired": {
```
のように、'tags'という項目を追加し、'personincharge'に担当者を追加すること。'application'も追加しておくこと。  
