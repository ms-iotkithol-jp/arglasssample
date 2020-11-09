# Stream Analytics Job 設定 
IoT Hub を通じて dynaEdge から送られてきたメッセージを処理し、条件に合致したメッセージについて後段のサービスに加工したメッセージを送信、あるいは、Blob Storage に蓄積する。  
Stream Analytics の作成、設定、実行に関する基本は、[「Azure Stream Analytics とは」](https://docs.microsoft.com/ja-jp/azure/stream-analytics/stream-analytics-introduction)を参考にすること。

## How To 
構築方法は以下の通り。Azure Portal での作業を説明する。作業が終わったら、ジョブ実行を開始する。    
1. 入力の作成
2. 出力の作成
3. クエリーの作成 

### 1. 入力の作成  
入力は、以下の二つを作成する。  
- dynaedge
- jobsupport  

dynaedge は、IoT Hub をソースとして作成する。  
jobsupport は、Blob Storageに、'referencedata'というフォルダーを作成し、そのフォルダーに'targetinfo.csv'というファイルを予めアップロードし、そのファイルをパスパターンとして指定して、参照データの入力として作成する。  
'targetinfo.csv'は、以下の様な形式のCSVファイルで、
```csv
target,registediotdevice,command,argument
[target-name],[target-device-name],Alert,on
```
<i>[target-name]</i> は、[WpfAppJobTracking](../../WpfAppJobTracking)で指定する'target'と同じにすること。  
<i>[target-device-name]</i>は、[環境センシングハンズオン](https://github.com/ms-iotkithol-jp/environment-sensing-hands-on)の、Device SDKを使ったアプリを使う場合には、IoT Hubに登録した '<i>DeviceId</i>' を記載し、IoT Edgeを使う場合には、'<i>DeviceId</i>/BarometerSensing' というデバイスIDとモジュール名（/で区切る）で指定する。  
Alert、on は、そのままでよい。各自が作成したDirect Methodを使いたい場合にはそれに合わせて変えればよい。 


### 2. 出力の作成  
出力は、以下の5つを作成する。  
- sensingstore
- locationstore
- jobandlocationnotify 
- sensingnotify 
- target 

入力作成で言及した Blob Storage に、'location'、'sensor'という二つのフォルダーを作成し、それぞれ、'locationstore'、'sensingstore' の出力作成時に割り当てる。イベントシリアル化形式は'JSON'、フォーマットは'アレイ'を選択する。  
[「クイック スタート:Azure portal を使用したイベント ハブの作成」](https://docs.microsoft.com/ja-jp/azure/event-hubs/event-hubs-create)を参考に、Event Hub 名前空間を一つ作成し、以下3つの Event Hub を作成する。 
- dynaedgejob -> jobandlocationnotify
- dynaedgesensing -> sensingnotify
- dynaedgecommand -> target

これら全ての Event Hub に共有アクセスポリシーで、'send'(送信)、'listen'(リッスン)というロールを作成し、上のリストに示すように、それぞれの出力として割り当てる。その際、Stream Analytics からメッセージを送信することになるので、ポリシーは'send'を選択すること。また、'イベントシリアル化形式'は'JSON'を、'フォーマット'は、'アレイ'を選択すること。  

### 3. クエリーの作成 
[query.txt](./query.txt) を Stream Analytics のクエリエディターにコピペし保存する。

以上で、設定は終わり。作成した Stream Analytics Job を実行すると、dynaEdge から送られてきたメッセージの種類によって、Blob Storage への保存、Event Hub へのメッセージ送信が行われる。  
- jobandlocationnotify -> dynaedgejob -> [JobNotifycation](../JobNotification)
- target -> dynaedgecommand -> [CollaborateWithIoTDevice](../CollaborateWithIoTDevice)

のように、Event Hub に送信されたメッセージは、それぞれ Azure Functionで実装されたサーバーレス実行ロジックのトリガーとなる。  
※ dynaedgesensing は、現時点では単に Event Hub に送信するだけである。将来拡張予定。  
