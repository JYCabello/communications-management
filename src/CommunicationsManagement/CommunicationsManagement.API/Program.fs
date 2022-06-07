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
open Routes


let (>>=>) a b = a >=> warbler (fun _ -> b)

let webApp (ports: IPorts) =
  choose [ GET
           >=> choose [ route "/login" >>=> login ports
                        route "/ping" >=> text "pong"
                        route "/inventory" >>=> json state
                        route "/" >>=> home ports ]
           POST
           >=> choose [ route "/login" >>=> loginPost ports ]]

let configureApp (app: IApplicationBuilder) ports = app.UseGiraffe <| webApp ports

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

let buildHost ports forcedPort =
  triggerSubscriptions ports

  Host
    .CreateDefaultBuilder()
    .ConfigureWebHostDefaults(fun webHostBuilder ->
      webHostBuilder
        .Configure(fun app -> configureApp app ports)
        .ConfigureServices(configureServices)
      |> ignore

      match forcedPort with
      | Some n ->
        webHostBuilder.UseUrls($"http://localhost:%i{n}")
        |> ignore
      | None -> ()

      webHostBuilder |> ignore)
    .Build()

let ports: IPorts =
  let config = Configuration.configuration

  { new IPorts with
      member this.sendEvent p = () |> TaskResult.ok
      member this.sendNotification p = () |> TaskResult.ok
      member this.configuration = config
      member this.query id = Storage.query config id
      member this.save a = Storage.save config a }

[<EntryPoint>]
let main _ =
  (buildHost ports None).Run()
  0
