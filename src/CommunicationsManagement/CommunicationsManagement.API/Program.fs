module Main

open System
open System.Collections.Concurrent
open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Ports
open CommunicationsManagement.API.Routing
open CommunicationsManagement.API.Routing.Routes.Rendering
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe


let webApp (ports: IPorts) =
  let solve = solveHandler ports
  let (>==>) a b = a >=> warbler (fun _ -> solve b)
  let routeCifE path h = routeCif path (h >> solve)

  choose [ GET
           >=> choose [ route "/login" >==> Login.get
                        route "/login/confirm" >==> Login.confirm
                        route "/logout" >==> Login.logout
                        route "/users" >==> Users.list
                        route "/users/create" >==> Users.createGet
                        routeCifE "/users/%O" Users.details
                        routeCifE "/users/%O/roles/add/%i" Users.addRole
                        routeCifE "/users/%O/roles/remove/%i" Users.removeRole
                        route "/channels" >==> Channels.list
                        route "/channels/create" >==> Channels.createGet
                        routeCifE "/channels/%O/enable" Channels.enableChannel
                        routeCifE "/channels/%O/disable" Channels.disableChannel
                        route "/communication-requests"
                        >==> CommReqRoute.list
                        route "/communication-requests/create"
                        >==> CommReqRoute.create
                        route "/" >==> Home.home ]
           POST
           >=> choose [ route "/login" >==> Login.post
                        route "/users/create" >==> Users.createPost
                        route "/channels/create" >==> Channels.createPost ] ]

let configureApp (app: IApplicationBuilder) ports = app.UseGiraffe <| webApp ports

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

let configureWebHostDefaults forcedPort ports (whb: IWebHostBuilder) =
  whb
    .Configure(fun app -> configureApp app ports)
    .ConfigureServices(configureServices)
  |> ignore

  match forcedPort with
  | Some n ->
    whb.UseUrls($"http://localhost:%i{n}")
    |> ignore
  | None -> ()

let buildHost ports forcedPort =
  task {
    do! EventStore.triggerSubscriptions ports

    return
      Host
        .CreateDefaultBuilder()
        .ConfigureWebHostDefaults(configureWebHostDefaults forcedPort ports)
        .Build()
  }

let ports config : IPorts =
  let storage =
    { Users = ConcurrentDictionary<Guid, User>()
      Sessions = ConcurrentDictionary<Guid, Session>()
      Channels = ConcurrentDictionary<Guid, Channel>()
      EditingCommunicationsRequests = ConcurrentDictionary<Guid, EditingCommunicationsRequest>() }

  { new IPorts with
      member this.sendEvent p = EventStore.sendEvent config p
      member this.sendNotification tr p = Notifications.send config p tr
      member this.configuration = config
      member this.find<'a> id = Storage.query<'a> storage config id

      member this.query<'a> predicate =
        Storage.queryPredicate<'a> storage config predicate

      member this.save<'a> a = Storage.save<'a> storage config a
      member this.delete<'a> id = Storage.delete<'a> storage config id
      member this.getAll<'a>() = Storage.getAll<'a> storage config () }

[<EntryPoint>]
let main _ =
  task {
    let! host = (buildHost (ports Configuration.configuration) None)
    host.Run()
    return 0
  }
  |> fun t -> t.Result
