module CommunicationsManagement.API.Routes

open System
open System.Globalization
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views
open CommunicationsManagement.Internationalization
open Microsoft.AspNetCore.Http
open Giraffe.Core
open Giraffe.ViewEngine
open Login

open type Giraffe.HttpContextExtensions

module Rendering =
  open type Giraffe.HttpContextExtensions

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
      let culture = getCulture ctx
      Translation.ResourceManager.GetString(key, culture)

  let private getSessionID (ctx: HttpContext) : Effect<Guid> =
    effect {
      return!
        (if ctx.Request.Headers.ContainsKey("sessionID") then
           match ctx.Request.Headers["sessionID"] |> Guid.TryParse with
           | true, id -> Ok id
           | false, _ -> NotAuthenticated |> Error
         else
           NotAuthenticated |> Error)
    }

  let authenticate (ctx: HttpContext) : Effect<ViewModelRoot> =
    effect {
      let! sessionID = getSessionID ctx
      let! session = fun p -> p.query<Session> sessionID
      let! user = fun p -> p.query<User> session.UserID
      let tr = getTranslator ctx

      return
        { User = Some user
          Title = tr "AppName"
          Translate = tr }
    }

  let getAnonymousRootModel (ctx: HttpContext) : ViewModelRoot =
    let tr = getTranslator ctx

    { User = None
      Title = tr "AppName"
      Translate = tr }

  let requireRole (user: User) (role: Roles) (resourceName: string) : Effect<unit> =
    fun _ ->
      (if user.hasRole role then
         Ok()
       else
         resourceName |> Unauthorized |> Error)
      |> Task.FromResult

  type Render<'a> = ViewModel<'a> -> XmlNode list

  let renderText (vm: ViewModel<string>) = [ Text vm.Model ]

  let renderHtml (view: XmlNode) : HttpHandler = htmlView view

  let renderOk (model: ViewModel<'a>) (view: Render<'a>) : HttpHandler =
    model
    |> (view >> Layout.layout model.Root)
    |> renderHtml

  let processError (e: DomainError) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
    let tr = getTranslator ctx
    let errorView = Layout.error tr >> htmlView

    (match e with
     | NotAuthenticated -> redirectTo false "/login"
     | Conflict -> [ "ConflictTemplate" |> tr |> Text ] |> errorView
     | NotFound en ->
       [ String.Format("NotFoundTextTemplate", en)
         |> tr
         |> Text ]
       |> errorView
     | Unauthorized rn ->
       [ String.Format("UnauthorizedTemplate", rn)
         |> tr
         |> Text ]
       |> errorView
     | InternalServerError e ->
       [ String.Format("InternalServerErrorTemplate", e)
         |> tr
         |> Text ]
       |> errorView
     | BadRequest ->
       [ "BadRequestTemplate" |> tr |> Text ]
       |> errorView)
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

open Rendering

module Login =
  [<CLIMutable>]
  type LoginDto = { Email: string Option }

  let get (ports: IPorts) : HttpHandler =
    fun next ctx ->
      effect {
        return
          { Model = { Email = None; EmailError = None }
            Root = getAnonymousRootModel ctx }
      }
      |> resolveEffect2 ports loginView next ctx

  let post (ports: IPorts) : HttpHandler =
    fun next ctx ->
      effect {
        let! dto =
          ctx.TryBindFormAsync<LoginDto>()
          |> FsToolkit.ErrorHandling.TaskResult.mapError (fun _ -> BadRequest)
          |> fromTR

        let rm = getAnonymousRootModel ctx

        let emailError =
          match DataValidation.isValidEmail dto.Email with
          | true -> None
          | false -> "InvalidEmail" |> rm.Translate |> Some

        return
          { Model =
              { Email = dto.Email
                EmailError = emailError }
            Root = rm }
      }
      |> resolveEffect2 ports loginView next ctx

let home (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! root = authenticate ctx

    return { Model = "Meh"; Root = root }
  }
  |> resolveEffect2 ports renderText next ctx
