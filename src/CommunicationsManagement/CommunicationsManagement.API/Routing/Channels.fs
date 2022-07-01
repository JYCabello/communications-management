[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Channels

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.Views.Channels
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Core
open Effects
open Giraffe
open System
open Flurl

let list: EffectRoute<HttpHandler> =
  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! vmr = getModelRoot
    let! channels = getAll<Channel>

    return
      renderOk
        ListChannels.list
        { Model = { Channels = channels }
          Root = vmr }
  }

let createGet: EffectRoute<HttpHandler> =
  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! vmr = getModelRoot

    return
      renderOk
        CreateChannel.create
        { Root = vmr
          Model = { Name = None; NameError = None } }
  }

[<CLIMutable>]
type CreateChannelPostDto = { Name: string option }

type private ValidationResult =
  | Valid of string
  | Invalid of ViewModel<CreateChannel.ChannelCreationViewModel>

let createPost: EffectRoute<HttpHandler> =
  let validate: EffectRoute<ValidationResult> =
    let existingError trx name (p: IPorts) _ _ =
      p.query<Channel> (fun c -> c.Name = name)
      |> Task.map (function
        | Ok _ -> "AlreadyExists" |> trx |> Some |> Ok
        | Error error ->
          match error with
          | NotFound _ -> Ok None
          | e -> Error e)

    effectRoute {
      let! dto = fromForm<CreateChannelPostDto>
      let! vmr = getModelRoot

      let emptyError =
        match String.IsNullOrWhiteSpace(dto.Name |> Option.defaultValue "") with
        | true -> "CannotBeEmpty" |> vmr.Translate |> Some
        | false -> None

      let! existingError =
        match dto.Name with
        | Some n -> existingError vmr.Translate n
        | None -> effectRoute { return None }

      let error = emptyError |> Option.orElse existingError

      return
        match error with
        | None -> Valid dto.Name.Value
        | Some e ->
          Invalid
            { Root = vmr
              Model = { Name = dto.Name; NameError = Some e } }
    }

  let save name =
    effectRoute {
      let! vmr = getModelRoot

      do!
        emit
          { Event =
              ChannelCreated
                { ChannelID = Guid.NewGuid()
                  ChannelName = name } }

      let url =
        vmr
          .BaseUrl
          .AppendPathSegment("channels")
          .ToString()

      return redirectTo true url
    }

  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! validation = validate

    return!
      match validation with
      | Valid s -> save s
      | Invalid m -> effectRoute { return renderOk CreateChannel.create m }
  }

let private switchChannel id eventBuilder : EffectRoute<HttpHandler> =
  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! vmr = getModelRoot
    let! channel = find<Channel> id
    do! emit { Event = eventBuilder channel }

    return
      redirectTo
        false
        (vmr
          .BaseUrl
          .AppendPathSegment("channels")
          .ToString())
  }

let enableChannel id : EffectRoute<HttpHandler> =
  switchChannel id (fun c -> ChannelEnabled { ChannelID = c.ID })

let disableChannel id : EffectRoute<HttpHandler> =
  switchChannel id (fun c -> ChannelDisabled { ChannelID = c.ID })
