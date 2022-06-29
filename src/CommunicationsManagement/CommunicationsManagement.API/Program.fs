module Main

open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Routing
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe


let webApp (ports: IPorts) =
  let solve er = solveHandler ports er
  let (>==>) a b = a >=> warbler (fun _ -> solve b)

  let routeCifE path routeHandler =
    routeCif path (fun t -> routeHandler t |> solve)

  choose [ GET
           >=> choose [ route "/login" >==> Login.get
                        route "/login/confirm" >==> Login.confirm
                        route "/logout" >==> Login.logout
                        route "/users" >==> Users.list
                        route "/users/create" >==> Users.create
                        routeCifE "/users/%O" Users.details
                        routeCifE "/users/%O/roles/add/%i" (fun (userId, role) ->
                          Users.addRole userId role)
                        routeCifE "/users/%O/roles/remove/%i" (fun (userId, role) ->
                          Users.removeRole userId role)
                        route "/" >==> Home.home ]
           POST
           >=> choose [ route "/login" >==> Login.post
                        route "/users/create" >==> Users.createPost ] ]

let configureApp (app: IApplicationBuilder) ports =
  app.UseGiraffe <| webApp ports
  //let localizationOptions = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>()
  app.UseRequestLocalization() |> ignore

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

let buildHost ports forcedPort =
  EventStore.triggerSubscriptions ports

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
  (buildHost (ports Configuration.configuration) None)
    .Run()

  0
