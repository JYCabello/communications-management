[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Users

open System
open CommunicationsManagement.API
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Views.Users
open FsToolkit.ErrorHandling
open Giraffe
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.DataValidation
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes

let list =
  effectRoute {
    let! root = getModelRoot
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
    let! root = getModelRoot
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

type UserCreationValidation =
  | UserValid of UserCreated
  | UserInvalid of CreateUser.UserCreationViewModel

let createPost =
  let validate (dto: CreateUserDto) (p: IPorts) : TaskEffectValidateResult<UserCreated> =
    taskEffValid {
      let! email =
        match validateEmail2 (nameof dto.Email) dto.Email with
        | Invalid ve -> EffectValidate.invalid ve
        | Valid email ->
          p.query<User> (fun u -> u.Email = Email email)
          |> Task.bind (fun r ->
            r
            |> function
              | Ok _ -> EffectValidate.validationError (nameof dto.Email) "EmailAlreadyInUse"
              | Error err ->
                match err with
                | NotFound _ -> EffectValidate.valid email
                | e -> EffectValidate.fail e)

      and! name =
        match dto.Name with
        | None -> EffectValidate.validationError (nameof dto.Name) "CannotBeEmpty"
        | Some n ->
          if String.IsNullOrWhiteSpace(n)
          then EffectValidate.validationError (nameof dto.Name) "CannotBeEmpty"
          else EffectValidate.valid n

      and! roles =
        match dto.Roles with
        | None -> EffectValidate.validationError (nameof dto.Roles) "MustSelectOneOption"
        | Some r -> EffectValidate.valid r

      return
        { Name = name
          Email = email
          UserID = Guid.NewGuid()
          Roles = roles }
    }

  let save usr : EffectRoute<HttpHandler> =
    effectRoute {
      do! emit { Event = UserCreated usr }
      let! url = buildUrl [ "users" ] []
      return! renderSuccess url
    }

  effectRoute {
    let! vmr = getModelRoot
    do! requireRole Roles.UserManagement
    let! dto = fromForm<CreateUserDto>
    let! vr = validate dto

    return!
      match vr with
      | Valid userCreated -> save userCreated
      | Invalid ve ->
        effectRoute {
          let errorFor n = errorFor n ve vmr.Translate
          let nameError = errorFor (nameof dto.Name)
          let emailError = errorFor (nameof dto.Email)
          let rolesError = errorFor (nameof dto.Roles)

          let (model: CreateUser.UserCreationViewModel) =
            { Name = dto.Name
              NameError = nameError
              Email = dto.Email
              EmailError = emailError
              Roles = dto.Roles |> Option.defaultValue Roles.None
              RolesError = rolesError }

          return renderOk CreateUser.createUserView { Model = model; Root = vmr }
        }
  }

let details id =
  effectRoute {
    let! root = getModelRoot
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
