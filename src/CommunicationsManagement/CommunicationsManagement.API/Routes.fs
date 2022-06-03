module CommunicationsManagement.API.Routes

open System.Net
open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.views
open Microsoft.AspNetCore.Http
open Giraffe.Core
open Giraffe.ViewEngine
open Effects
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

let resolve
  ports
  (ctx: HttpContext)
  (render: Render<'a>)
  (e: Effect<'a>)
  : Task<HttpContext option> =
  task {
    let! result = e ports |> attempt

    let bytes, code =
      match result with
      | Ok a ->
        a
        |> (render >> layout)
        |> (fun n -> n, HttpStatusCode.OK)
      | Error e -> e |> renderError
      |> fun (n, c) -> (RenderView.AsBytes.htmlNode n, c)

    code
    |> LanguagePrimitives.EnumToValue
    |> ctx.SetStatusCode

    do ctx.SetContentType "text/html; charset=utf-8"
    do! ctx.WriteBytesAsync bytes :> Task
    return None
  }

let login (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect { return Some ctx }
  |> resolve ports ctx fakeRender
  |> fun _ -> next ctx

let home (ports: IPorts) (_: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect { return Some ctx }
  |> resolve ports ctx fakeRender
