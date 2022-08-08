[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Home

open CommunicationsManagement.API
open CommunicationsManagement.API.Routing.Routes
open CommunicationsManagement.API.Routing.Routes.Rendering
open Giraffe
open Models
open Effects

let home: EffectRoute<HttpHandler> =
  rail {
    let! root = modelRoot
    return renderOk Views.Home.homeView { Model = (); Root = root }
  }
