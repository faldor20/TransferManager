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
    interface IDataGetter
    {

    }
    public class DataGetter
    {
        public static Uri baseAddress;
        HttpClient http = new HttpClient { BaseAddress = baseAddress };
        public Dictionary<string, Dictionary<string, List<TransferData>>> CopyTasks = null;
        public List<(string group, string user, TransferData task)> AllTasks = null;
        public bool first=true;
        public Dictionary<string, Dictionary<string, List<TransferData>>> GroupTasks = null;
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
        public async Task StartService()
        {
            Console.WriteLine("Started data retrieval service");
            transferServerUrl = await http.GetStringAsync("/WeatherForecast");
            Console.WriteLine("data=" + transferServerUrl);

            hubConnection = new HubConnectionBuilder()
                .WithUrl(transferServerUrl + "/datahub")
                .AddMessagePackProtocol()
                .Build();

            hubConnection.On<Dictionary<string, Dictionary<string, List<TransferData>>>>("ReceiveData", dataList =>
            {
                //this is necissary to convert the time into local time from utc because when sending datetime strings using signalR Time gets cnverted to utc
                var res = dataList.Select(pair =>
                    (pair.Key,
                    pair.Value.Select(pair2 =>
                        (pair2.Key,
                             pair2.Value.Select(data =>
                                {
                                    data.StartTime = data.StartTime.ToLocalTime(); data.ScheduledTime = data.ScheduledTime.ToLocalTime(); data.EndTime = data.EndTime.ToLocalTime();
                                    return data;
                                 }).ToList()
                        )
                    )
                    ));
                var dic=  res.ToDictionary(x => x.Key, x => x.Item2.ToDictionary(y => y.Key, y => y.Item2));
           /*      if(first){
                    first=false;
                   CopyTasks=dic;
                }
                else{
                foreach (var group in dic)
                {

                    foreach (var user in group.Value)
                    {

                       for (int i = 0; i < user.Value.Count; i++)
                       {
                           if(CopyTasks[group.Key]?[user.Key]?[i]?.EndTime!=user.Value[i].EndTime)
                                CopyTasks[group.Key][user.Key][i]=user.Value[i];
                       }
                    }
                }
                } */
                CopyTasks=dic;
               /*  CopyTasks = res.ToDictionary(x => x.Key, x => x.Item2.ToDictionary(y => y.Key, y => y.Item2));
                CopyTasks.Distinct */
                //Logging: Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(dataList));

                status = Status.Connected;

                newData.Invoke();


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
        Task Send() =>
           hubConnection.SendAsync("GetTransferData");
        Task Confirm() =>
           hubConnection.SendAsync("GetConfirmation");

        async Task ContinuousSend()
        {

            Console.WriteLine("starting Data requests");
            while (true)
            {
                if (IsConnected)
                {
                    //	await Confirm();
                    await Send();


                }
                else
                {
                    Console.WriteLine("notconnected via SignalR");
                }
                await Task.Delay(500);
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