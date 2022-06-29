module CommunicationsManagement.API.Routing.Routes

open System
open System.Globalization
open System.Threading.Tasks
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

  let buildModelRoot (ctx: HttpContext) : Effect<ViewModelRoot> =
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

  let requireRole (role: Roles) (resourceName: string) ctx : Effect<unit> =
    effect {
      let! user = auth ctx

      return!
        (if user.hasRole role then
           Ok()
         else
           resourceName |> Unauthorized |> Error)
        |> Task.FromResult
    }

  type Render<'a> = ViewModel<'a> -> XmlNode list

  let renderText (vm: ViewModel<string>) = [ Text vm.Model ]

  let renderHtml (view: XmlNode) : HttpHandler = htmlView view

  let renderOk (model: ViewModel<'a>) (view: Render<'a>) : HttpHandler =
    model
    |> (view >> Layout.layout model.Root)
    |> renderHtml

  let renderOk2 (view: Render<'a>) (model: ViewModel<'a>) : HttpHandler =
    model
    |> (view >> Layout.layout model.Root)
    |> renderHtml

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
       |> errorView
     | EarlyReturn h -> h)
    |> fun handler -> handler next ctx

  let resolveEffect
    (ports: IPorts)
    (view: Render<'a>)
    (e: Effect<ViewModel<'a>>)
    (next: HttpFunc)
    (ctx: HttpContext)
    : Task<HttpContext option> =
    task {
      let! result = e ports |> attempt

      return!
        match result with
        | Ok model -> renderOk model view next ctx
        | Error error -> processError error next ctx
    }

  // Exists just for the cases where the context is explicit in the route definition
  let resolveEffect2 ports view next ctx eff = resolveEffect ports view eff next ctx

  let resolveRedirect p (getUrl: IPorts -> 'a -> string) next ctx (e: Effect<'a>) : HttpFuncResult =
    task {
      let! r = e p

      return!
        match r |> Result.map (getUrl p) with
        | Ok url -> redirectTo false url next ctx
        | Error error -> processError error next ctx
    }

  let theVoid: Render<'a> = fun _ -> []

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

    member inline this.Source(ce: HttpHandler) : EffectRoute<HttpHandler> =
      fun _ _ _ -> TaskResult.ok ce

    member inline this.Source(ce: HttpContext -> Task<Result<'a, DomainError>>) : EffectRoute<'a> =
      fun _ _ c -> c |> ce

    member inline this.Source(e: Effect<'a>) : EffectRoute<'a> = fun p _ _ -> e p

    member inline this.Source(a: Task<'a>) : EffectRoute<'a> = fun _ _ _ -> a |> Task.map Ok

    member inline this.Source(t: Task) : EffectRoute<unit> =
      fun _ _ _ ->
        task {
          do! t
          return! TaskResult.ok ()
        }

    member inline this.Source(a: Result<'a, DomainError>) : EffectRoute<'a> =
      fun _ _ _ -> a |> Task.singleton

  let effectRoute = EffectRouteBuilder()

  let getPorts: EffectRoute<IPorts> = fun p _ _ -> TaskResult.ok p

  open Rendering

  let resolveTR view next ctx tr =
    task {
      let! r = tr

      return!
        match r with
        | Ok model -> renderOk model view next ctx
        | Error error -> processError error next ctx
    }

  let resolveER view ports next ctx er =
    solve ports next ctx er |> resolveTR view next ctx

  let resolveERRedirect getUrl p n c e : HttpFuncResult =
    task {
      let! r = e p n c

      return!
        match r |> Result.map (getUrl p) with
        | Ok url -> redirectTo false url
        | Error error -> processError error
        |> (fun r -> r n c)
    }

  let solveHandler (p: IPorts) (er: EffectRoute<HttpHandler>) : HttpHandler =
    fun n c ->
      task {
        let! tr = er p n c

        return!
          match tr with
          | Ok h -> h
          | Error e -> processError e
          |> (fun r -> r n c)
      }

  let bindForm (c: HttpContext) =
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

  let emit e (p: IPorts) =
    p.sendEvent e
  
  let notify n : EffectRoute<unit> =
    effectRoute {
      let! rm = getAnonymousRootModel
      do! (fun (p: IPorts) -> p.sendNotification rm.Translate n)
    }
