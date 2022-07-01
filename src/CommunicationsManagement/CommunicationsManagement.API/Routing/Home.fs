[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Home

open CommunicationsManagement.API
open CommunicationsManagement.API.Routing.Routes
open CommunicationsManagement.API.Routing.Routes.Rendering
open Giraffe
open Models
open EffectfulRoutes

let home =
  effectRoute {
    let! root = getModelRoot
    return! renderOk Views.Home.homeView { Model = (); Root = root }
  }
