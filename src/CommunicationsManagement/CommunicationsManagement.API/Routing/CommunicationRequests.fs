﻿[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.CommunicationRequests

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