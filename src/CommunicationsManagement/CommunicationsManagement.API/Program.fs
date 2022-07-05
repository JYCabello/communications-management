module Main

open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Routing
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open FsToolkit.ErrorHandling
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
                        route "/" >==> Home.home ]
           POST
           >=> choose [ route "/login" >==> Login.post
                        route "/users/create" >==> Users.createPost
                        route "/channels/create" >==> Channels.createPost ] ]

let configureApp (app: IApplicationBuilder) ports = app.UseGiraffe <| webApp ports

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

let buildHost ports forcedPort =
  task {
    do! EventStore.triggerSubscriptions ports

    return
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
  }

let ports config : IPorts =
  { new IPorts with
      member this.sendEvent p = EventStore.sendEvent config p
      member this.sendNotification tr p = Notifications.send config p tr
      member this.configuration = config
      member this.find<'a> id = Storage.query<'a> config id

      member this.query<'a> predicate =
        Storage.queryPredicate<'a> config predicate

      member this.save<'a> a = Storage.save<'a> config a
      member this.delete<'a> id = Storage.delete<'a> config id
      member this.getAll<'a>() = Storage.getAll<'a> config () }

[<EntryPoint>]
let main _ =
  task {
    let! host = (buildHost (ports Configuration.configuration) None)
    host.Run()
    return 0
  }
  |> fun t -> t.Result
