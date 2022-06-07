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
open Login

open type Giraffe.HttpContextExtensions

module Rendering =
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

  let renderHtml (view: XmlNode) : HttpHandler = htmlView view

  let renderOk (model: ViewModel<'a>) (view: Render<'a>) : HttpHandler =
    model
    |> (view >> layout model.Root)
    |> fun v -> renderHtml v

  let processError (e: DomainError) : HttpHandler =
    match e with
    | NotAuthenticated -> redirectTo false "/login"
    | _ -> failwith "not implemented"

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
    effect {
      return
        { Model = { Email = None; EmailError = None }
          Root = { Title = None; User = None } }
    }
    |> resolveEffect ports loginView

  let post (ports: IPorts) : HttpHandler =
    fun next ctx ->
      effect {
        let! dto =
          ctx.TryBindFormAsync<LoginDto>()
          |> FsToolkit.ErrorHandling.TaskResult.mapError (fun s -> BadRequest)
          |> fromTR

        let emailError =
          match dto.Email with
          | Some e ->
            match e with
            | "" -> Some "Email cannot be empty"
            | _ -> None
          | None -> Some "Email cannot be empty"

        return
          { Model =
              { Email = dto.Email
                EmailError = emailError }
            Root = { Title = None; User = None } }
      }
      |> resolveEffect2 ports loginView next ctx

let home (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! user = authenticate ctx

    return
      { Model = "Meh"
        Root = { Title = None; User = Some user } }
  }
  |> resolveEffect2 ports renderText next ctx
