[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Channels

open System.ComponentModel.DataAnnotations
open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.EffectValidation
open CommunicationsManagement.API.Views.Channels
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Core
open Effects
open System


[<CLIMutable>]
type CreateChannelPostDto = { Name: string option }

type private ValidationResult2 =
  | Valid of string
  | Invalid2 of ViewModel<CreateChannel.ChannelCreationViewModel>

let list =
  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! vmr = getModelRoot
    let! channels = getAll<Channel>

    return!
      renderOk
        ListChannels.list
        { Model = { Channels = channels }
          Root = vmr }
  }

let createGet =
  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! vmr = getModelRoot

    return!
      renderOk
        CreateChannel.create
        { Root = vmr
          Model = { Name = None; NameError = None } }
  }
  
let validatetv (p: IPorts) (dto: CreateChannelPostDto) : TaskValidation<string> =
  taskValidation {
    let emptyError =
      [{ FieldName = nameof dto.Name; Error = "CannotBeEmpty" }]
      |> Invalid
      |> TaskResult.error
    let alreadyExistsError =
      [ { FieldName = nameof dto.Name; Error = "AlreadyExists" } ]
      |> Invalid
      |> TaskResult.error

    let! name =
      match dto.Name with
      | None -> emptyError
      | Some n ->
        if String.IsNullOrWhiteSpace n
        then emptyError
        else n |> TaskResult.ok

    and! _ =
      match dto.Name with
      | None -> emptyError
      | Some name ->
        p.query<Channel> (fun c -> c.Name = name)
        |> Task.bind (function
          | Ok _ -> alreadyExistsError
          | Error error ->
            match error with
            | NotFound _ -> TaskResult.ok name
            | e -> e |> Fail |> TaskResult.error)

    return name
  }

let createPost =
  let validate: EffectRoute<ValidationResult2> =
    let existingError trx name =
      effectRoute {
        let! p = getPorts

        return!
          p.query<Channel> (fun c -> c.Name = name)
          |> Task.map (function
            | Ok _ -> "AlreadyExists" |> trx |> Some |> Ok
            | Error error ->
              match error with
              | NotFound _ -> Ok None
              | e -> Error e)
      }

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
          Invalid2
            { Root = vmr
              Model = { Name = dto.Name; NameError = Some e } }
    }

  let save name =
    effectRoute {
      do!
        emit
          { Event =
              ChannelCreated
                { ChannelID = Guid.NewGuid()
                  ChannelName = name } }

      let! returnUrl = buildUrl [ "channels" ] []
      return! renderSuccess returnUrl
    }

  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! validation = validate

    let! dto = fromForm<CreateChannelPostDto>
    let! p = getPorts
    let! (v2: string) = validatetv p dto

    return!
      match validation with
      | Valid s -> save s
      | Invalid2 m -> effectRoute { return renderOk CreateChannel.create m }
  }

let private switchChannel id eventBuilder =
  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! channel = find<Channel> id
    do! emit { Event = eventBuilder channel }

    let! returnUrl = buildUrl [ "channels" ] []

    return! renderSuccess returnUrl
  }

let enableChannel id =
  switchChannel id (fun c -> ChannelEnabled { ChannelID = c.ID })

let disableChannel id =
  switchChannel id (fun c -> ChannelDisabled { ChannelID = c.ID })
