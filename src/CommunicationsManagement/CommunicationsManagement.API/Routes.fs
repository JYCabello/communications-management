module CommunicationsManagement.API.Routes

open System
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views
open Microsoft.AspNetCore.Http
open Giraffe.Core
open Giraffe.ViewEngine
open Layout
open LoginModels

open type Giraffe.HttpContextExtensions

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

let authenticate (ctx: HttpContext) : Effect<User> =
  effect {
    let! sessionID = getSessionID ctx
    let! session = fun p -> p.query<Session> sessionID
    return! fun p -> p.query<User> session.UserID
  }

type Render<'a> = ViewModel<'a> -> XmlNode list

let renderText (vm: ViewModel<string>) = [ Text vm.Model ]

let renderHtml (view: XmlNode) : HttpHandler =
  htmlView view

let renderOk
  (model: ViewModel<'a>)
  (view: Render<'a>)
  : HttpHandler =
  model
  |> (view >> layout model.Root)
  |> fun v -> renderHtml v

let processError (e: DomainError) (next: HttpFunc)  (ctx: HttpContext): Task<HttpContext option> =
  match e with
  | NotAuthenticated -> redirectTo false "/login" next ctx
  | _ -> failwith "not implemented"

let renderEffect
  (ports: IPorts)
  (view: Render<'a>)
  (next: HttpFunc)
  (ctx: HttpContext)
  (e: Effect<ViewModel<'a>>)
  : Task<HttpContext option> =
    let r = e ports
    task {
      let! result = r
      return!
        match result with
          | Ok model -> renderOk model view next ctx
          | Error error -> processError error next ctx
    }



let login (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    return
      { Model = { Email = None; EmailError = None }
        Root = { Title = None; User = None } }
  }
  |> renderEffect ports Login.view next ctx

let loginPost (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    return
      { Model = { Email = None; EmailError = None }
        Root = { Title = None; User = None } }
  }
  |> renderEffect ports Login.view next ctx

let home (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! user = authenticate ctx

    return
      { Model = "Meh"
        Root = { Title = None; User = Some user } }
  }
  |> renderEffect ports renderText next ctx
