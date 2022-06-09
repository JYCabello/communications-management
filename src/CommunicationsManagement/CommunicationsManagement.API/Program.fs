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
open EventStore
open Routes


let (>>=>) a b = a >=> warbler (fun _ -> b)

let webApp (ports: IPorts) =
  choose [ GET
           >=> choose [ route "/login" >>=> Login.get ports
                        route "/login/confirm" >>=> Login.confirm ports
                        route "/logout" >>=> Login.logout ports
                        route "/ping" >=> text "pong"
                        route "/inventory" >>=> json state
                        route "/" >>=> home ports ]
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
      member this.sendNotification p = Notifications.send config p
      member this.configuration = config
      member this.query id = Storage.query config id
      member this.find predicate = Storage.queryPredicate config predicate
      member this.save a = Storage.save config a
      member this.delete id = Storage.delete config id }

[<EntryPoint>]
let main _ =
  (buildHost ports None).Run()
  0
