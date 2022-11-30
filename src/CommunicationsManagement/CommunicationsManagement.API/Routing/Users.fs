﻿[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Users

open System
open CommunicationsManagement.API
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Ports
open CommunicationsManagement.API.Views.Users
open FsToolkit.ErrorHandling
open Giraffe
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.DataValidation
open EffectRouteOps

let list: RailRoute<HttpHandler> =
  rail {
    let! root = modelRoot
    do! requireRole Roles.UserManagement
    let! users = getAll<User>

    return
      renderOk
        ListUsers.usersListView
        { Model = { Users = users }
          Root = root }
  }

let createGet: RailRoute<HttpHandler> =
  rail {
    let! root = modelRoot
    do! requireRole Roles.UserManagement

    return
      renderOk
        CreateUser.createUserView
        { Model =
            { Name = None
              NameError = None
              Email = None
              EmailError = None
              Roles = Roles.None
              RolesError = None }
          Root = root }
  }

[<CLIMutable>]
type CreateUserDto =
  { Name: string option
    Email: string option
    Roles: Roles option }

let createPost =
  let validate (dto: CreateUserDto) (p: IPorts) : TaskEffectValidateResult<UserCreated> =
    effectValidation {
      let! email =
        match validateEmail (nameof dto.Email) dto.Email with
        | Invalid ve -> EffectValidate.invalid ve
        | Valid email ->
          validateNotExisting<User, Email> (fun u -> u.Email = email) (nameof dto.Email) email p

      and! name =
        match dto.Name with
        | None -> EffectValidate.validationError (nameof dto.Name) "CannotBeEmpty"
        | Some n ->
          match n with
          | WithValue s -> EffectValidate.valid s
          | WhiteSpace | Empty ->
            EffectValidate.validationError (nameof dto.Name) "CannotBeEmpty"

      and! roles =
        match dto.Roles with
        | None -> EffectValidate.validationError (nameof dto.Roles) "MustSelectOneOption"
        | Some r -> EffectValidate.valid r

      return
        { Name = name
          Email =
            email
            |> function
              | Email e -> e
          UserID = Guid.NewGuid()
          Roles = roles }
    }

  let save usr : RailRoute<HttpHandler> =
    rail {
      do! emit { Event = UserCreated usr }
      let! url = buildUrl [ "users" ] []
      return! renderSuccess url
    }

  let renderErrors ve dto : RailRoute<HttpHandler> =
    rail {
      let! vmr = modelRoot
      let errorFor n = errorFor n ve vmr.Translate

      let (model: CreateUser.UserCreationViewModel) =
        { Name = dto.Name
          NameError = errorFor (nameof dto.Name)
          Email = dto.Email
          EmailError = errorFor (nameof dto.Email)
          Roles = dto.Roles |> Option.defaultValue Roles.None
          RolesError = errorFor (nameof dto.Roles) }

      return renderOk CreateUser.createUserView { Model = model; Root = vmr }
    }

  rail {
    do! requireRole Roles.UserManagement
    let! dto = fromForm<CreateUserDto>
    let! vr = validate dto

    return!
      match vr with
      | Valid userCreated -> save userCreated
      | Invalid ve -> renderErrors ve dto
  }

let details id =
  rail {
    let! root = modelRoot
    do! requireRole Roles.UserManagement
    let! user = find<User> id

    let! regularUser =
      match user with
      | Admin _ -> "Admin" |> root.Translate |> Unauthorized |> Error
      | Regular ru -> Ok ru

    return renderOk UserDetails.details { Model = regularUser; Root = root }
  }

let private switchRole (userId, role) eventBuilder =
  let noneToBadRequest =
    function
    | Some r -> Ok r
    | None -> BadRequest |> Error

  rail {
    do! requireRole Roles.UserManagement
    let! root = modelRoot
    let! userCandidate = find<User> userId

    let! user =
      match userCandidate with
      | Admin _ -> "Admin" |> root.Translate |> Unauthorized |> Error
      | Regular ru -> Ok ru

    let! role =
      Enum.GetValues<Roles>()
      |> Seq.tryFind (fun r -> (r |> int) = role)
      |> noneToBadRequest

    do! emit { Event = (user, role) |> eventBuilder }
    let! url = buildUrl [ "users"; user.ID ] []
    return! renderSuccess url
  }

let addRole userIdRole =
  switchRole userIdRole (fun (u, r) -> RoleAdded { UserID = u.ID; RoleToAdd = r })

let removeRole userIdRole =
  switchRole userIdRole (fun (u, r) -> RoleRemoved { UserID = u.ID; RoleRemoved = r })
