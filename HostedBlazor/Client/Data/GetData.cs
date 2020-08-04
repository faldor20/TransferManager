namespace HostedBlazor.Data
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System;
    using blaz.Data;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.AspNetCore.SignalR;
    using static SharedFs.SharedTypes;
    using System.Net.Http;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.AspNetCore.SignalR.Protocol;
    using System.Timers;
    interface IDataGetter
    {

    }
    public class DataGetter
    {
        public static Uri baseAddress;
        HttpClient http = new HttpClient { BaseAddress = baseAddress };
        public Dictionary<string, Dictionary<string, Dictionary<int, TransferData>>> CopyTasks = null;
        public List<(string group, string user, TransferData task)> AllTasks = null;
        public bool first = true;
        public bool GotFirstConnectData = false;
        public Dictionary<string, Dictionary<string, Dictionary<int, Action>>> ComponentUpdateEvents = new Dictionary<string, Dictionary<string, Dictionary<int, Action>>>();
        public string transferServerUrl;
        public Status status = Status.Loading;
        public event Action newData;
        private HubConnection hubConnection;
        /*   public void UpDateAllTasks(){
              AllTasks=CopyTasks.SelectMany(
                      group => group.Value.SelectMany(
                          user => user.Value.Select(
                              task=>(group:group.Key,user:user.Key,task)))
                  ).OrderBy(data => data.task.ScheduledTime).ToList();
          } */
        TransferData ConvertToLocalTime(TransferData transData)
        {
            transData.StartTime = transData.StartTime.ToLocalTime();
            transData.ScheduledTime = transData.ScheduledTime.ToLocalTime();
            transData.EndTime = transData.EndTime.ToLocalTime();
            return transData;
        }
        public async Task StartService()
        {
            Console.WriteLine("Started data retrieval service");
            transferServerUrl = await http.GetStringAsync("/WeatherForecast");
            Console.WriteLine("data=" + transferServerUrl);

            hubConnection = new HubConnectionBuilder()
                .WithUrl(transferServerUrl + "/datahub")
                .AddMessagePackProtocol()
                .Build();

            hubConnection.On<Dictionary<string, Dictionary<string, Dictionary<int, TransferData>>>>("ReceiveData", dataList =>
           {
               //this is necissary to convert the time into local time from utc because when sending datetime strings using signalR Time gets cnverted to utc
               foreach (var group in dataList)
               {
                   foreach (var user in group.Value)
                   {
                       foreach (var i in user.Value)
                       {
                           dataList[group.Key][user.Key][i.Key].StartTime = i.Value.StartTime.ToLocalTime();
                           dataList[group.Key][user.Key][i.Key].ScheduledTime = i.Value.ScheduledTime.ToLocalTime();
                           dataList[group.Key][user.Key][i.Key].EndTime = i.Value.EndTime.ToLocalTime();
                       }
                   }
               }
                CopyTasks=dataList;
               status = Status.Connected;

               newData.Invoke();
           });
            hubConnection.On<string, Dictionary<string, Dictionary<int, TransferData>>>("ReceiveDataChange", (user, change) =>
             {
                 lock (CopyTasks)
                 {
                     Console.WriteLine("recived changes from ClientManager");
                     var fullRefresh = false;
                     foreach (var group in change)
                     {
                         foreach (var index in group.Value)
                         {

                             if (!CopyTasks.ContainsKey(group.Key))
                             {
                                 CopyTasks[group.Key] = new Dictionary<string, Dictionary<int, TransferData>>();
                                 fullRefresh = true;
                             }
                             if (!CopyTasks[group.Key].ContainsKey(user))
                             {
                                 CopyTasks[group.Key][user] = new Dictionary<int, TransferData>();
                                 fullRefresh = true;
                             }
                             if (!CopyTasks[group.Key][user].ContainsKey(index.Key)) fullRefresh = true;
                             var transferData = ConvertToLocalTime(index.Value);
                             CopyTasks[group.Key][user][index.Key] = transferData;
                             //If a fullrefresh is going to ccur anyway this would be pointless
                             if (!fullRefresh)
                                 ComponentUpdateEvents[group.Key][user][index.Key].Invoke();
                         }
                     }
                     if (fullRefresh)
                     {
                         newData.Invoke();
                     }

                 }



             });
            /*   hubConnection.On<Object> ("ReceiveData", dataList => {

                  Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(dataList));

                  status = Status.Connected;
                  newData.Invoke();
              });  */

            hubConnection.On<string>("Testing", confirmed => { Console.WriteLine(confirmed); status = Status.Connected; });
            await hubConnection.StartAsync();

            await ContinuousSend();

        }
        public Task Cancel(string groupName, string userName, int id) =>
            hubConnection.SendAsync("CancelTransfer", groupName, userName, id);
        Task RequestData() =>
           hubConnection.SendAsync("GetTransferData");
        Task Confirm() =>
           hubConnection.SendAsync("GetConfirmation");

        async Task ContinuousSend()
        {
            var timer = new System.Timers.Timer(1000 * 60);
            timer.Elapsed += (caller, arg) =>{
                RequestData();
                
                };
            timer.Start();
            Console.WriteLine("starting Data requests");
            while (true)
            {
                if (IsConnected)
                {
                    if (!GotFirstConnectData)
                    {
                        GotFirstConnectData = true;
                        //	await Confirm();
                        await RequestData();
                    }
                }
                else
                {
                    Console.WriteLine("notconnected via SignalR");
                }
                await Task.Delay(5000);
            }

        }

        public bool IsConnected =>
            hubConnection.State == HubConnectionState.Connected;

        public void Dispose()
        {
            _ = hubConnection.DisposeAsync();
        }
    }
}