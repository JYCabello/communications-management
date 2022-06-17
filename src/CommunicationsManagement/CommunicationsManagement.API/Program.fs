module Main

open System.Collections.Generic
open System.Globalization
open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Routing
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Localization
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe


let (>>=>) a b = a >=> warbler (fun _ -> b)

let webApp (ports: IPorts) =
  choose [ GET
           >=> choose [ route "/login" >>=> Login.get ports
                        route "/login/confirm" >>=> Login.confirm ports
                        route "/logout" >>=> Login.logout ports
                        route "/ping" >=> text "pong"
                        route "/" >>=> Home.home ports ]
           POST
           >=> choose [ route "/login" >>=> Login.post ports ] ]

let configureApp (app: IApplicationBuilder) ports =
  app.UseGiraffe <| webApp ports
  //let localizationOptions = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>()
  app.UseRequestLocalization() |> ignore
  ()

let configureServices (services: IServiceCollection) =
  services.AddGiraffe() |> ignore

  services.Configure<RequestLocalizationOptions> (fun (opt: RequestLocalizationOptions) ->
    let supportedCultures = [ CultureInfo("es"); CultureInfo("en") ] |> List
    opt.DefaultRequestCulture <- RequestCulture(CultureInfo("es"), CultureInfo("es"))
    opt.SupportedCultures <- supportedCultures
    opt.SupportedUICultures <- supportedCultures
    ())
  |> ignore

  ()

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
      member this.query<'a> id = Storage.query<'a> config id

      member this.find<'a> predicate =
        Storage.queryPredicate<'a> config predicate

      member this.save<'a> a = Storage.save<'a> config a
      member this.delete<'a> id = Storage.delete<'a> config id
      member this.getAll<'a>() = Storage.getAll<'a> config () }

[<EntryPoint>]
let main _ =
  (buildHost (ports Configuration.configuration) None)
    .Run()

  0
