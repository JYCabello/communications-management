[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
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
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open EffectOps

let list =
  effectRoute {
    let! root = modelRoot
    do! requireRole Roles.UserManagement
    let! users = getAll<User>

    return!
      renderOk
        ListUsers.usersListView
        { Model = { Users = users }
          Root = root }
  }

let createGet =
  effectRoute {
    let! root = modelRoot
    do! requireRole Roles.UserManagement

    return!
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
          if String.IsNullOrWhiteSpace(n) then
            EffectValidate.validationError (nameof dto.Name) "CannotBeEmpty"
          else
            EffectValidate.valid n

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

  let save usr : EffectRoute<HttpHandler> =
    effect {
      do! EffectRouteOps.emit { Event = UserCreated usr }
      let! url = buildUrl [ "users" ] []
      return! renderSuccess url
    }

  let renderErrors ve dto : EffectRoute<HttpHandler> =
    effect {
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

  effectRoute {
    do! requireRole Roles.UserManagement
    let! dto = fromForm<CreateUserDto>
    let! vr = validate dto

    return!
      match vr with
      | Valid userCreated -> save userCreated
      | Invalid ve -> renderErrors ve dto
  }

let details id =
  effectRoute {
    let! root = modelRoot
    do! requireRole Roles.UserManagement
    let! user = find<User> id
    return renderOk UserDetails.details { Model = user; Root = root }
  }


let switchRole (userId, role) eventBuilder =
  effectRoute {
    do! requireRole Roles.UserManagement
    let! user = find<User> userId

    let! role =
      Enum.GetValues<Roles>()
      |> Seq.tryFind (fun r -> (r |> int) = role)
      |> (function
      | Some r -> Ok r
      | None -> BadRequest |> Error)

    do! emit { Event = (user, role) |> eventBuilder }
    let! url = buildUrl [ "users"; user.ID ] []
    return! renderSuccess url
  }

let addRole userIdRole =
  switchRole userIdRole (fun (u, r) -> RoleAdded { UserID = u.ID; RoleToAdd = r })

let removeRole userIdRole =
  switchRole userIdRole (fun (u, r) -> RoleRemoved { UserID = u.ID; RoleRemoved = r })
