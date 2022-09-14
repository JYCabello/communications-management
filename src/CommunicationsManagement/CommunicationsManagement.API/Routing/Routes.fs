module CommunicationsManagement.API.Routing.Routes

open System
open System.Globalization
open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Ports
open CommunicationsManagement.API.Views
open CommunicationsManagement.Internationalization
open FsToolkit.ErrorHandling
open Giraffe
open Microsoft.AspNetCore.Http
open Giraffe.ViewEngine
open EffectRouteOps

open type HttpContextExtensions


module Rendering =
  open type HttpContextExtensions

  let context: RailRoute<HttpContext> = fun (_, _, c: HttpContext) -> TaskResult.ok c

  let configuration: RailRoute<Configuration> =
    fun (p: IPorts, _, _) -> TaskResult.ok p.configuration

  let getCulture (ctx: HttpContext) : CultureInfo =
    let defaultCulture () =
      (if ctx.Request.Headers.ContainsKey("Accept-Language") then
         ctx
           .Request
           .Headers[ "Accept-Language" ]
           .ToString()
         |> Some
       else
         None)
      |> Option.bind (fun h ->
        if h.StartsWith("en") then
          CultureInfo "en" |> Some
        else
          None)
      |> Option.defaultValue (CultureInfo "es")

    let validCultureNames = [ "es"; "en" ]

    let changeHeader =
      if ctx.Request.Query.ContainsKey("setLang") then
        validCultureNames
        |> Seq.tryFind (fun c -> c = ctx.Request.Query[ "setLang" ].ToString())
      else
        None

    changeHeader
    |> Option.iter (fun c -> ctx.Response.Cookies.Append("Selected-Language", c))

    let cookieLang =
      if ctx.Request.Cookies.ContainsKey("Selected-Language") then
        validCultureNames
        |> Seq.tryFind (fun c ->
          c = ctx
            .Request
            .Cookies[ "Selected-Language" ]
            .ToString())
      else
        None

    changeHeader
    |> Option.orElseWith (fun () -> cookieLang)
    |> Option.map CultureInfo
    |> Option.defaultWith defaultCulture

  let getTranslator (ctx: HttpContext) : Translator =
    fun key ->
      match Translation.ResourceManager.GetString(key, getCulture ctx) with
      | null -> $"[%s{key}]"
      | "" -> $"[%s{key}]"
      | t -> t

  let translator: RailRoute<Translator> =
    rail {
      let! ctx = context
      return getTranslator ctx
    }

  let getSessionID: RailRoute<Guid> =
    rail {
      let! ctx = context

      return!
        (if ctx.Request.Cookies.ContainsKey("sessionID") then
           match ctx.Request.Cookies["sessionID"] |> Guid.TryParse with
           | true, id -> Ok id
           | false, _ -> NotAuthenticated |> Error
         else
           NotAuthenticated |> Error)
    }

  let getUrl =
    rail {
      let! ctx = context

      return
        $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.Path}{ctx.Request.QueryString.Value}"
    }

  let auth: RailRoute<User> =
    rail {
      let! sessionID = getSessionID

      let! session =
        fun (p: IPorts, _, _) ->
          p.find<Session> sessionID
          |> TaskResult.mapError (function
            | NotFound _ -> NotAuthenticated
            | e -> e)
          |> TaskResult.bind (fun s ->
            match DateTime.UtcNow < s.ExpiresAt with
            | true -> TaskResult.ok s
            | false -> TaskResult.error NotAuthenticated)

      return!
        fun (p: IPorts, _, _) ->
          p.find<User> session.UserID
          |> TaskResult.mapError (function
            | NotFound _ -> NotAuthenticated
            | e -> e)
    }

  let modelRoot: RailRoute<ViewModelRoot> =
    rail {
      let! user = auth
      let! tr = translator
      let! config = configuration
      let! url = getUrl

      return
        { User = Some user
          Title = tr "AppName"
          Translate = tr
          CurrentUrl = url
          BaseUrl = config.BaseUrl }
    }

  let getAnonymousRootModel: RailRoute<ViewModelRoot> =
    rail {
      let! tr = translator
      let! config = fun (p: IPorts, _, _) -> p.configuration |> TaskResult.ok
      let! url = getUrl

      return
        { User = None
          Title = tr "AppName"
          Translate = tr
          CurrentUrl = url
          BaseUrl = config.BaseUrl }
    }

  type Render<'a> = ViewModel<'a> -> XmlNode list

  let renderOk (view: Render<'a>) (model: ViewModel<'a>) : HttpHandler =
    model
    |> (view >> Layout.layout model.Root)
    |> htmlView

  let processError (e: DomainError) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
    let tr = getTranslator ctx
    let errorView = Layout.notification tr >> htmlView

    (match e with
     | NotAuthenticated -> redirectTo false "/login"
     | Conflict -> [ "ConflictTemplate" |> tr |> Text ] |> errorView
     | NotFound en ->
       [ String.Format(tr "NotFoundTextTemplate", en)
         |> Text ]
       |> errorView
     | Unauthorized rn ->
       [ String.Format(tr "UnauthorizedTemplate", rn)
         |> Text ]
       |> errorView
     | InternalServerError e ->
       [ String.Format(tr "InternalServerErrorTemplate", e)
         |> Text ]
       |> errorView
     | BadRequest ->
       [ "BadRequestTemplate" |> tr |> Text ]
       |> errorView)
    |> fun handler -> handler next ctx

  open Urls

  let errorFor name (errors: ValidateError list) (tr: Translator) =
    errors
    |> Seq.tryFind (fun e -> e.FieldName = name)
    |> Option.map (fun e -> tr e.Error)

  let buildUrl segments queryParams : RailRoute<string> =
    rail {
      let! ports = getPorts
      return urlFor ports.configuration.BaseUrl segments queryParams
    }

  let renderMsg m url : RailRoute<HttpHandler> =
    rail {
      let! vmr = modelRoot

      return
        htmlView (
          Layout.notificationReturn
            { Root = vmr
              Model = { Message = m; Url = url } }
        )
    }

  let renderSuccess url : RailRoute<HttpHandler> =
    rail {
      let! vmr = modelRoot
      return! renderMsg ("OperationSuccessful" |> vmr.Translate) url
    }

  let solveHandler (p: IPorts) (er: RailRoute<HttpHandler>) : HttpHandler =
    fun n c ->
      task {
        let! tr = er (p, n, c) |> attempt

        return!
          match tr with
          | Ok h -> h
          | Error e -> processError e
          |> (fun r -> r n c)
      }

  let fromForm<'a> =
    rail {
      let! ctx = context

      return!
        ctx.TryBindFormAsync<'a>()
        |> TaskResult.mapError (fun _ -> BadRequest)
    }

  let queryGuid name =
    rail {
      let! ctx = context

      return!
        ctx.TryGetQueryStringValue(name)
        |> Option.bind (fun c ->
          match Guid.TryParse c with
          | true, guid -> Some guid
          | false, _ -> None)
        |> function
          | Some c -> TaskResult.ok c
          | None -> TaskResult.error BadRequest
    }

  let setCookie name value =
    rail {
      let! ctx = context

      return!
        ctx.Response.Cookies.Append(name, value.ToString())
        |> TaskResult.ok
    }

  let notify n : RailRoute<unit> =
    rail {
      let! rm = getAnonymousRootModel
      do! fun (p: IPorts, _, _) -> p.sendNotification rm.Translate n
    }

  let requireRole (role: Roles) : RailRoute<unit> =
    rail {
      let! user = auth
      let! vmr = getAnonymousRootModel

      return!
        (match user.hasRole role with
         | true -> Ok()
         | false ->
           role
           |> getRoleName
           |> vmr.Translate
           |> Unauthorized
           |> Error)
        |> Task.FromResult
    }

  let requireOneRole (roles: Roles list) resourceName : RailRoute<unit> =
    rail {
      let! user = auth
      let! vmr = getAnonymousRootModel

      return!
        (match roles |> List.exists user.hasRole with
         | true -> Ok()
         | false ->
           resourceName
           |> vmr.Translate
           |> Unauthorized
           |> Error)
        |> Task.FromResult
    }
