[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.CommReqRoute

open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views.CommunicationRequests
open Giraffe
open EffectRouteOps
open CommunicationsManagement.API.Routing.Routes.Rendering

let list: RailRoute<HttpHandler> =
  rail {
    let! root = modelRoot
    do! requireOneRole [ Roles.Press; Roles.Delegate ] "CommunicationRequests"
    let! requestsInEdition = getAll<EditingCommunicationsRequest>

    return
      renderOk
        ListRequests.list
        { Root = root
          Model = { InEdition = requestsInEdition } }
  }

let create: RailRoute<HttpHandler> =
  rail {
    let! root = modelRoot
    do! requireRole Roles.Delegate

    return
      renderOk
        CreateRequest.create
        { Root = root
          Model = { Title = None; TitleError = None } }
  }
