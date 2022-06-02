module Main

open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open EventStore


let (>>=>) a b = a >=> warbler (fun _ -> b)

let webApp (_: IPorts) =
  choose [ route "/ping" >=> text "pong"
           route "/inventory" >>=> json state
           route "/" >=> htmlFile "./pages/index.html" ]

let configureApp (app: IApplicationBuilder) ports = app.UseGiraffe <| webApp ports

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

let buildHost ports forcedPort =
  triggerSubscriptions ports
  
  Host
    .CreateDefaultBuilder()
    .ConfigureWebHostDefaults(fun webHostBuilder ->
      webHostBuilder
        .Configure(fun app -> configureApp app ports)
        .ConfigureServices(configureServices) |> ignore
      
      match forcedPort with
      | Some n -> webHostBuilder.UseUrls($"http://localhost:%i{n}") |> ignore
      | None -> ()
      
      webHostBuilder
      |> ignore)
    .Build()

let ports: IPorts =
  { new IPorts with
      member this.sendEvent p = () |> TaskResult.ok
      member this.sendNotification p = () |> TaskResult.ok
      member this.configuration = Configuration.configuration }

[<EntryPoint>]
let main _ =
  (buildHost ports None).Run()
  0
