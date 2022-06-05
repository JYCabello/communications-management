module CommunicationsManagement.API.Routes

open System
open System.Net
open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Effects
open CommunicationsManagement.API.views
open Microsoft.AspNetCore.Http
open Giraffe.Core
open Giraffe.ViewEngine
open Layout

open type Giraffe.HttpContextExtensions

let private getSessionID (ctx: HttpContext) : Effect<Guid> =
  effect {
    let! id =
      (if ctx.Request.Headers.ContainsKey("sessionID") then
         match ctx.Request.Headers["sessionID"] |> Guid.TryParse with
         | true, id -> Ok id
         | false, _ -> NotAuthenticated |> Error
       else
         NotAuthenticated |> Error)
      |> fromResult

    return id
  }

let authenticate (ctx: HttpContext) : Effect<User> =
  effect {
    let! sessionID = getSessionID ctx
    let! ports = getPorts
    let! session = ports.query<Session> sessionID |> fromAR
    return! ports.query<User> session.UserID
  }

type Render<'a> = ViewModel<'a> -> XmlNode list

let renderText (vm: ViewModel<string>) = [ Text vm.Model ]

let renderHtml (ctx: HttpContext) (bytes: byte array) (code: HttpStatusCode) (next: HttpFunc) =
  task {
    do
      code
      |> LanguagePrimitives.EnumToValue
      |> ctx.SetStatusCode

    do ctx.SetContentType "text/html; charset=utf-8"
    do! ctx.WriteBytesAsync bytes :> Task
    return! next ctx
  }

let renderOk
  (ctx: HttpContext)
  (model: ViewModel<'a>)
  (view: Render<'a>)
  (next: HttpFunc)
  : Task<HttpContext option> =
  model
  |> (view >> layout model.Root)
  |> (fun n -> RenderView.AsBytes.htmlNode n, HttpStatusCode.OK)
  |> fun (bytes, code) -> renderHtml ctx bytes code next

let processError (ctx: HttpContext) (e: DomainError) (next: HttpFunc) : Task<HttpContext option> =
  task {
    let! x =
      match e with
      | NotAuthenticated -> redirectTo false "/login" next ctx
      | _ -> failwith "not implemented"

    return x
  }

let renderEffect
  (ports: IPorts)
  (ctx: HttpContext)
  (view: Render<'a>)
  (next: HttpFunc)
  (e: Effect<ViewModel<'a>>)
  : Task<HttpContext option> =
  task {
    let! result = e ports |> attempt

    return!
      match result with
      | Ok model -> renderOk ctx model view next
      | Error error -> processError ctx error next
  }

let login (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    return
      { Model = "Meh"
        Root = { Title = None; User = None } }
  }
  |> renderEffect ports ctx views.Login.login next

let home (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! user = authenticate ctx

    return
      { Model = "Meh"
        Root = { Title = None; User = Some user } }
  }
  |> renderEffect ports ctx renderText next
