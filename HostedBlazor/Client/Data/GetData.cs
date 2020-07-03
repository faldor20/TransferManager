namespace HostedBlazor.Data {
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
    interface IDataGetter{

    }
    public class DataGetter {
        public static Uri baseAddress;
        HttpClient http= new HttpClient{BaseAddress=baseAddress};
        public  Dictionary<string,List<TransferData>> CopyTasks = null;
        public  string transferServerUrl;
        public  Status status = Status.Loading;
        public  event Action newData;
        private  HubConnection hubConnection;
       public async Task StartService () {
            Console.WriteLine("Started data retrieval service");
            transferServerUrl = await http.GetStringAsync ("/WeatherForecast");
            Console.WriteLine ("data=" + transferServerUrl);

            hubConnection = new HubConnectionBuilder ()
                .WithUrl (transferServerUrl + "/datahub")
                .AddMessagePackProtocol() 
                .Build ();

             hubConnection.On<Dictionary<string,List<TransferData>>> ("ReceiveData", dataList => {
                
                CopyTasks= dataList;

               //Logging: Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(dataList));

                status = Status.Connected;
               
            newData.Invoke();
            });
          /*   hubConnection.On<Object> ("ReceiveData", dataList => {

                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(dataList));

                status = Status.Connected;
                newData.Invoke();
            });  */

            hubConnection.On<string> ("Testing", confirmed => { Console.WriteLine (confirmed); status = Status.Connected; });
            await hubConnection.StartAsync ();

            await ContinuousSend ();
        }
        public  Task Cancel (string groupName, int id) =>
            hubConnection.SendAsync ("CancelTransfer",groupName, id);
         Task Send () =>
            hubConnection.SendAsync ("GetTransferData");
         Task Confirm () =>
            hubConnection.SendAsync ("GetConfirmation");

         async Task ContinuousSend () {

            while (true) {
                if (IsConnected) {
                    //	await Confirm();
                    await Send ();
                    Console.WriteLine("Requesting data");

                } else {
                    Console.WriteLine ("notconnected via SignalR");
                }
                await Task.Delay (500);
            }

        }

        public  bool IsConnected =>
            hubConnection.State == HubConnectionState.Connected;

        public  void Dispose () {
            _ = hubConnection.DisposeAsync ();
        }
    }
}