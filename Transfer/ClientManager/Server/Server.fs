namespace ClientManager.Server
open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Saturn
open System.Threading;
open System.Threading.Tasks;
open Giraffe
open Microsoft.AspNetCore.SignalR
open Microsoft.AspNetCore.Hosting;
open Microsoft.Extensions.Hosting;
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection;
open Microsoft.AspNetCore.SignalR.Protocol;
module Server=
    let tryGetEnv key = 
        match Environment.GetEnvironmentVariable key with
        | x when String.IsNullOrWhiteSpace x -> None 
        | x -> Some x
    let port =
        "SERVER_PORT"
        |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us
  (*   let configureApp (app : IApplicationBuilder) =
        let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
        app.UseStaticFiles()
            .UseSignalR(fun routes -> routes.MapHub<GameHub>(PathString "/gameHub")) // SignalR
            .UseGiraffe(a)
 *)
    let configureServices (services : IServiceCollection) =
      services.AddCors()    |> ignore
                      // SignalR
      services.AddGiraffe() |> ignore

    let app = application {
        url ("http://*:" + port.ToString() + "/")
        
        memory_cache
        use_gzip
        
        (* use_cors("*")(fun builder-> 
            builder.AllowAnyHeader().AllowAnyOrigin().AllowAnyMethod()|>ignore 
            ()) *)
        service_config (fun services->
            services.AddSignalR().AddMessagePackProtocol()|>ignore
            services.AddSingleton<SignalR.ClientManager>()|>ignore;
            services.AddSingleton<SignalR.FrontEndManager>()|>ignore;
            ( services.AddCors((fun options -> options.AddPolicy("CorsPolicy",(fun builder ->
                    builder.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin()|>ignore
                    ()
                    )
                )
            )) )

            )
       (*  webhost_config(fun host->
            host.UseKestrel()
        ) *)
       
        app_config (fun app ->
            app.UseStaticFiles()
               .UseDefaultFiles()
               .UseRouting()
               
               .UseCors("CorsPolicy")
               .UseEndpoints(fun routes -> (routes.MapHub<SignalR.DataHub>("/datahub"))|>ignore) // SignalR
               .UseEndpoints(fun routes -> (routes.MapHub<SignalR.ClientManagerHub>("/ClientManagerHub"))|>ignore) // SignalR
            
        )
     
        
    }
