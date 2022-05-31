open System
open System.Collections.Generic
open System.IO
open System.Text
open System.Threading.Tasks
open CommunicationsManagement.API
open EventStore.Client
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Newtonsoft.Json
open Models

let configuration =
  ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .AddEnvironmentVariables()
    .Build()
    .Get<Configuration>()

let settings =
  EventStoreClientSettings.Create configuration.EventStoreConnectionString

let client = new EventStoreClient(settings)

let state = Dictionary<int, int>()

let mutable checkpoint: StreamPosition option = None

let getCheckpoint () =
  checkpoint
  |> Option.map FromStream.After
  |> Option.defaultValue FromStream.Start

let handleEvent (evnt: ResolvedEvent) =
  task {
    let data = evnt.Event.Data.ToArray()
    let decoded = Encoding.UTF8.GetString(data)
    let parsed = JsonConvert.DeserializeObject<Message>(decoded)

    if not <| state.ContainsKey(parsed.ID) then
      state[parsed.ID] <- parsed.Amount
    else
      state[parsed.ID] <- state[parsed.ID] + parsed.Amount

    checkpoint <- Some evnt.OriginalEventNumber

    return ()
  }

let rec subscribe () =
  let reSubscribe (_: StreamSubscription) (reason: SubscriptionDroppedReason) (_: Exception) =
    if reason = SubscriptionDroppedReason.Disposed |> not then
      subscribe () |> ignore
    else
      ()

  let handle _ evnt _ = task { do! handleEvent evnt } :> Task

  task {
    try
      return!
        client
          .SubscribeToStreamAsync("deletable", getCheckpoint(), handle, false, reSubscribe)
          :> Task
    with
    | _ ->
      do! Task.Delay(5000)
      return! subscribe ()
  }

subscribe () |> ignore

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
