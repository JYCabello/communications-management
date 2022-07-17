module CommunicationsManagement.API.Routing.Routes

open System
open System.Globalization
open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views
open CommunicationsManagement.Internationalization
open FsToolkit.ErrorHandling
open Giraffe
open Microsoft.AspNetCore.Http
open Giraffe.ViewEngine

open type HttpContextExtensions


module Rendering =
  open type HttpContextExtensions

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

  let getSessionID (ctx: HttpContext) : Effect<Guid> =
    effect {
      return!
        (if ctx.Request.Cookies.ContainsKey("sessionID") then
           match ctx.Request.Cookies["sessionID"] |> Guid.TryParse with
           | true, id -> Ok id
           | false, _ -> NotAuthenticated |> Error
         else
           NotAuthenticated |> Error)
    }

  let getUrl (ctx: HttpContext) =
    $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.Path}{ctx.Request.QueryString.Value}"

  let auth (ctx: HttpContext) : Effect<User> =
    effect {
      let! sessionID = getSessionID ctx

      let! session =
        fun p ->
          p.find<Session> sessionID
          |> TaskResult.mapError (function
            | NotFound _ -> NotAuthenticated
            | e -> e)
          |> TaskResult.bind (fun s ->
            match DateTime.UtcNow < s.ExpiresAt with
            | true -> TaskResult.ok s
            | false -> TaskResult.error NotAuthenticated)

      return!
        fun p ->
          p.find<User> session.UserID
          |> TaskResult.mapError (function
            | NotFound _ -> NotAuthenticated
            | e -> e)
    }

  let getModelRoot (ctx: HttpContext) : Effect<ViewModelRoot> =
    effect {
      let! user = auth ctx
      let tr = getTranslator ctx
      let! config = fun p -> p.configuration |> TaskResult.ok

      return
        { User = Some user
          Title = tr "AppName"
          Translate = tr
          CurrentUrl = getUrl ctx
          BaseUrl = config.BaseUrl }
    }

  let getAnonymousRootModel (ctx: HttpContext) : Effect<ViewModelRoot> =
    effect {
      let tr = getTranslator ctx
      let! config = fun p -> p.configuration |> TaskResult.ok

      return
        { User = None
          Title = tr "AppName"
          Translate = tr
          CurrentUrl = getUrl ctx
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

module EffectfulRoutes =
  type EffectRoute<'a> = IPorts -> HttpFunc -> HttpContext -> Task<Result<'a, DomainError>>

  let mapER (f: 'a -> 'b) (e: EffectRoute<'a>) : EffectRoute<'b> =
    fun p next ctx -> e p next ctx |> TaskResult.map f

  let bindER (f: 'a -> EffectRoute<'b>) (e: EffectRoute<'a>) : EffectRoute<'b> =
    fun p next ctx ->
      taskResult {
        let! a = e p next ctx
        let ber = f a |> mapER id
        return! ber p next ctx
      }

  let toEffectRoute h : EffectRoute<'a> = fun _ _ _ -> TaskResult.ok h

  let solve p n c er : Task<Result<'a, DomainError>> = er p n c

  type EffectRouteBuilder() =
    member inline this.Bind
      (
        e: EffectRoute<'a>,
        [<InlineIfLambda>] f: 'a -> EffectRoute<'b>
      ) : EffectRoute<'b> =
      bindER f e

    member inline this.Return a : EffectRoute<'a> = fun _ _ _ -> TaskResult.ok a
    member inline this.ReturnFrom(e: EffectRoute<'a>) : EffectRoute<'a> = e
    member inline this.Zero() : EffectRoute<Unit> = fun _ _ _ -> TaskResult.ok ()

    member inline this.Combine(a: EffectRoute<'a>, b: EffectRoute<'b>) : EffectRoute<'b> =
      a |> bindER (fun _ -> b)

    member inline this.Source(er: EffectRoute<'a>) : EffectRoute<'a> = er

    member inline this.Source(ce: HttpContext -> Effect<'a>) : EffectRoute<'a> =
      fun p _ c -> c |> ce |> (fun e -> e p)

    member inline this.Source(h: HttpHandler) : EffectRoute<HttpHandler> =
      fun _ _ _ -> TaskResult.ok h

    member inline this.Source(ce: HttpContext -> Task<Result<'a, DomainError>>) : EffectRoute<'a> =
      fun _ _ c -> c |> ce

    member inline this.Source(e: Effect<'a>) : EffectRoute<'a> = fun p _ _ -> e p

    member inline this.Source<'a when 'a: not struct>(a: Task<'a>) : EffectRoute<'a> =
      fun _ _ _ -> a |> Task.map Ok

    member inline this.Source(a: Task<Result<'a, DomainError>>) : EffectRoute<'a> = fun _ _ _ -> a

    member inline this.Source(t: Task) : EffectRoute<unit> =
      fun _ _ _ ->
        task {
          do! t
          return! TaskResult.ok ()
        }

    member inline this.Source(a: Result<'a, DomainError>) : EffectRoute<'a> =
      fun _ _ _ -> a |> Task.singleton

    member inline _.Source
      (pvr: IPorts -> TaskEffectValidateResult<'a>)
      : EffectRoute<ValidateResult<'a>> =
      fun p _ _ ->
        task {
          let! vr = pvr p

          return
            match vr with
            | EffectValid a -> a |> Valid |> Ok
            | EffectInvalid ve -> ve |> Invalid |> Ok
            | EffectFail de -> de |> Error
        }

  let effectRoute = EffectRouteBuilder()

  open Rendering
  open Urls

  let errorFor name (errors: ValidateError list) (tr: Translator) =
    errors
    |> Seq.tryFind (fun e -> e.FieldName = name)
    |> Option.map (fun e -> tr e.Error)

  let buildUrl segments queryParams : EffectRoute<string> =
    effectRoute {
      let! ports = getPorts
      return urlFor ports.configuration.BaseUrl segments queryParams
    }

  let renderMsg m url : EffectRoute<HttpHandler> =
    effectRoute {
      let! vmr = getModelRoot

      return!
        htmlView (
          Layout.notificationReturn
            { Root = vmr
              Model = { Message = m; Url = url } }
        )
    }

  let renderSuccess url : EffectRoute<HttpHandler> =
    effectRoute {
      let! vmr = getModelRoot
      return! renderMsg ("OperationSuccessful" |> vmr.Translate) url
    }

  let solveHandler (p: IPorts) (er: EffectRoute<HttpHandler>) : HttpHandler =
    fun n c ->
      task {
        let! tr = er p n c |> attempt

        return!
          match tr with
          | Ok h -> h
          | Error e -> processError e
          |> (fun r -> r n c)
      }

  let fromForm<'a> (c: HttpContext) =
    c.TryBindFormAsync<'a>()
    |> TaskResult.mapError (fun _ -> BadRequest)

  let queryGuid name (c: HttpContext) =
    c.TryGetQueryStringValue(name)
    |> Option.bind (fun c ->
      match Guid.TryParse c with
      | true, guid -> Some guid
      | false, _ -> None)
    |> function
      | Some c -> TaskResult.ok c
      | None -> TaskResult.error BadRequest

  let setCookie name value (c: HttpContext) =
    c.Response.Cookies.Append(name, value.ToString())
    |> TaskResult.ok

  let notify n : EffectRoute<unit> =
    effectRoute {
      let! rm = getAnonymousRootModel
      do! fun (p: IPorts) -> p.sendNotification rm.Translate n
    }

  let requireRole (role: Roles) : EffectRoute<unit> =
    effectRoute {
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
