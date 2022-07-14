[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Channels

open System.Linq.Expressions
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Views.Channels
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Core
open System


[<CLIMutable>]
type CreateChannelPostDto = { Name: string option }

type private ValidationResult2 =
  | Valid2 of string
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

let createPost =
  let validate (dto: CreateChannelPostDto) (p: IPorts) : TaskEffectValidateResult<string> =
    taskEffValid {
      let! name =
        match dto.Name with
        | None -> EffectValidate.validationError (nameof dto.Name) "CannotBeEmpty"
        | Some n ->
          if String.IsNullOrWhiteSpace n then
            EffectValidate.validationError (nameof dto.Name) "CannotBeEmpty"
          else
            n |> EffectValidate.valid

      do!
        p.query<Channel> (fun c -> c.Name = name)
        |> Task.bind (function
          | Ok _ -> EffectValidate.validationError (nameof dto.Name) "AlreadyExists"
          | Error error ->
            match error with
            | NotFound _ -> EffectValidate.valid ()
            | e -> e |> EffectValidate.fail)

      return name
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

  let renderValidationErrors dto ve =
    effectRoute {
      let! vmr = getModelRoot

      return
        renderOk
          CreateChannel.create
          { Root = vmr
            Model =
              { Name = dto.Name
                NameError = errorFor (nameof dto.Name) ve vmr.Translate } }
    }

  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! dto = fromForm<CreateChannelPostDto>
    let! validateResult = validate dto

    return!
      match validateResult with
      | Valid s -> save s
      | Invalid ve -> renderValidationErrors dto ve
  }

let private switchChannel eventBuilder id =
  effectRoute {
    do! requireRole Roles.ChannelManagement
    let! channel = find<Channel> id
    do! emit { Event = eventBuilder channel }
    let! returnUrl = buildUrl [ "channels" ] []
    return! renderSuccess returnUrl
  }

let enableChannel id =
  switchChannel (fun c -> ChannelEnabled { ChannelID = c.ID }) id

let disableChannel id =
  switchChannel (fun c -> ChannelDisabled { ChannelID = c.ID }) id
