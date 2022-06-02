module Main

open CommunicationsManagement.API
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open EventStore


let (>>=>) a b = a >=> warbler (fun _ -> b)

let webApp =
  choose [ route "/ping" >=> text "pong"
           route "/inventory" >>=> json state
           route "/" >=> htmlFile "./pages/index.html" ]

let configureApp (app: IApplicationBuilder) = app.UseGiraffe webApp

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

let buildHost () =
  Host
    .CreateDefaultBuilder()
    .ConfigureWebHostDefaults(fun webHostBuilder ->
      webHostBuilder
        .Configure(configureApp)
        .ConfigureServices(configureServices)
      |> ignore)
    .Build()

[<EntryPoint>]
let main _ =
  buildHost().Run()
  0
