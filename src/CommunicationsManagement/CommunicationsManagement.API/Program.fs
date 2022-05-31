open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open CommunicationsManagement.API
open EventStore.Client
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open EventStore


let (>>=>) a b =
  a >=> warbler (fun _ -> b)

let webApp =
  choose [ route "/ping" >=> text "pong"
           route "/inventory" >>=> json state
           route "/" >=> htmlFile "./pages/index.html" ]

let configureApp (app: IApplicationBuilder) = app.UseGiraffe webApp

let configureServices (services: IServiceCollection) =
  services.AddGiraffe() |> ignore

[<EntryPoint>]
let main _ =
  Host
    .CreateDefaultBuilder()
    .ConfigureWebHostDefaults(fun webHostBuilder ->
      webHostBuilder
        .Configure(configureApp)
        .ConfigureServices(configureServices)
      |> ignore)
    .Build()
    .Run()

  0
