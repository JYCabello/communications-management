[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Home

open CommunicationsManagement.API
open CommunicationsManagement.API.Routing.Routes
open CommunicationsManagement.API.Routing.Routes.Rendering
open Giraffe
open Models
open EffectfulRoutes
open Views.Home

let home: EffectRoute<HttpHandler> =
  effectRoute {
    let! root = buildModelRoot
    return renderOk2 homeView { Model = (); Root = root }
  }
