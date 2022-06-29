[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Home

open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Routing.Routes.Rendering
open Microsoft.AspNetCore.Http
open Giraffe
open Models
open Views.Home

let home (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! root = buildModelRoot ctx

    return { Model = (); Root = root }
  }
  |> resolveEffect2 ports homeView next ctx
