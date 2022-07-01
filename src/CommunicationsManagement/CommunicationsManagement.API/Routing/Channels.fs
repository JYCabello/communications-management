[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Channels

open CommunicationsManagement.API
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.Views.Channels
open Models
open Effects
open Giraffe

let list: EffectRoute<HttpHandler> =
  effectRoute {
    let! vmr = buildModelRoot
    let! channels = getAll<Channel>

    return
      renderOk
        ListChannels.list
        { Model = { Channels = channels }
          Root = vmr }
  }

let createGet: EffectRoute<HttpHandler> =
  effectRoute { return redirectTo true "/" }

let createPost: EffectRoute<HttpHandler> =
  effectRoute { return redirectTo true "/" }
