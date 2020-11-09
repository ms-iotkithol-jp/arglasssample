# dynaEdge の Job 状態変更通知 を SignalR でブロードキャストする  
dynaEdge が Job の Requesting を Device Twins の Desired Properties の更新で認知し、ユーザーが声で、'開始'
 と命令したときに、IoT Hub に、要求されたジョブに対して 'InResponse’ になったことをメッセージ通知したとき、または、'完了'
  と表明したときに 'Done' になったことをメッセージ痛した時に、Stream Analytics Job によって、'jobandlocationnotify' Event Hub にメッセージが転送されたときに起動される。  
  ロジック内で、SignalR をキックするEvent Hubに転送され、その SignalR にサブスクライブしている、'[WpfAppJobTracking](../../WpfAppJobTracking)' にそのメッセージが転送され、要求したジョブの状態表示が変更される。  

## How To 
このロジックを実行するにはまず、[https://github.com/ms-iotkithol-jp/IoTDataShareBySignalRService](https://github.com/ms-iotkithol-jp/IoTDataShareBySignalRService) で、SignalR サービスを作る必要がある。
### ローカルテスト実行 
VS Codeを使って開発用PC上でローカル実行・デバッグが可能である。
'[local.settings.json](./local.settings.json)'の各項目にそれぞれの接続文字列をコピペして保存し、
- AzureWebJobsStorage　-> Stream Analytics で使った Blob Storage と同じ Storage Account の接続文字列
- trigger_EVENTHUB -> Stream Analytics の出力 'target' に割り当てた Event Hub の、'listen'ポリシーの接続文字列

- destination_EVENTHUB -> [https://github.com/ms-iotkithol-jp/IoTDataShareBySignalRService](https://github.com/ms-iotkithol-jp/IoTDataShareBySignalRService) の Func1 のトリガーになっている Event Hub の送信ロールの接続文字列


### Azure への発行
VS Code で、このフォルダーを開き、[「Publish the project to Azure」](https://docs.microsoft.com/ja-jp/azure/azure-functions/create-first-function-vs-code-csharp#publish-the-project-to-azure)を参考に、Azure にロジックを公開する。
Azure Portal で公開した Function を開き、'構成'->'アプリケーション設定' で、
上述した、local.settings.json の 'AzureWebJobsStorage'、'trigger_EVENTHUB'、'destination_EVENTHUB' を '+新しいアプリケーション設定' で追加する。

後は、Stream Analytics Jobを実行して、WpfAppJsonTrackingでジョブ要求を作成し、dynaEdge で操作を行えば、この Function のロジックが実行し、Job 状態変更通知が受けられる。 
※ dynaEdge の上で動いている [UWPIoTApp](../../UWPIoTAIApp) は、ジョブ状態変更だけでなく、位置情報も送信している。Stream Analytics Job は、ジョブの状態変更だけでなく、位置情報も本ロジックに送信してきているので、結果的に SignalR で位置情報もブロードキャストされる。  
将来的には、Azure Maps 等でこの位置情報通知についても、現在位置の表示などの拡張を行う予定である。  
 

