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

type Render<'a> = 'a -> XmlNode list

let fakeRender _ = []

let renderError =
  function
  | InternalServerError _ -> (html [] [], HttpStatusCode.InternalServerError)
  | Unauthorized _ -> (html [] [], HttpStatusCode.Unauthorized)
  | NotAuthenticated -> (html [] [], HttpStatusCode.Unauthorized)
  | NotFound _ -> (html [] [], HttpStatusCode.NotFound)
  | Conflict -> (html [] [], HttpStatusCode.Conflict)

let renderHtml (ctx: HttpContext) (bytes: byte array) (code: HttpStatusCode) (next: HttpFunc) =
  task {
    code
    |> LanguagePrimitives.EnumToValue
    |> ctx.SetStatusCode

    do ctx.SetContentType "text/html; charset=utf-8"
    do! ctx.WriteBytesAsync bytes :> Task
    return! next ctx
  }

let renderOk
  (ctx: HttpContext)
  (model: 'a)
  (view: Render<'a>)
  (next: HttpFunc)
  (vmr: ViewModelRoot)
  : Task<HttpContext option> =
  model
  |> (view >> layout vmr)
  |> (fun n -> RenderView.AsBytes.htmlNode n, HttpStatusCode.OK)
  |> fun (bytes, code) -> renderHtml ctx bytes code next

let processError (ctx: HttpContext) (e: DomainError) (next: HttpFunc) : Task<HttpContext option> =
  failwith "meh"

let renderEffect
  (ports: IPorts)
  (ctx: HttpContext)
  (view: Render<'a>)
  (next: HttpFunc)
  (title: string option)
  (e: Effect<'a>)
  : Task<HttpContext option> =
  task {
    let! result = e ports |> attempt

    let vmr =
      { User =
          { Name = ""
            Roles = Roles.Press
            Email = Email "meh"
            ID = Guid.Empty }
        Title = title }

    return!
      match result with
      | Ok model -> renderOk ctx model view next vmr
      | Error error -> processError ctx error next
  }

let login (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect { return Some ctx }
  |> renderEffect ports ctx fakeRender next None

let home (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect { return Some ctx }
  |> renderEffect ports ctx fakeRender next None
