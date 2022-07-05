[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Users

open System
open System.Threading.Tasks
open CommunicationsManagement.API
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
  | Valid of UserCreated
  | Invalid of CreateUser.UserCreationViewModel

let createPost =
  let validate (p: IPorts) (dto: CreateUserDto) (tr: Translator) : Task<UserCreationValidation> =
    task {
      let emailError = validateEmail dto.Email tr

      let! emailExists =
        match emailError with
        | Some _ -> false |> Task.FromResult
        | None ->
          match dto.Email with
          | None -> true |> Task.FromResult
          | Some email ->
            p.query<User> (fun u -> u.Email = Email email)
            |> Task.map (fun r ->
              r
              |> function
                | Ok _ -> true
                | Error _ -> false)

      let emailError' =
        if emailExists then
          "EmailAlreadyInUse" |> tr |> Some
        else
          emailError

      let nameError =
        match String.IsNullOrWhiteSpace(dto.Name |> Option.defaultValue "") with
        | true -> "CannotBeEmpty" |> tr |> Some
        | false -> None

      let rolesError =
        match dto.Roles with
        | None -> "MustSelectOneOption" |> tr |> Some
        | _ -> None

      let hasErrors =
        emailError'.IsSome
        || nameError.IsSome
        || rolesError.IsSome

      return
        match hasErrors with
        | true ->
          Invalid
            { Name = dto.Name
              NameError = nameError
              Email = dto.Email
              EmailError = emailError'
              Roles = dto.Roles |> Option.defaultValue Roles.None
              RolesError = rolesError }
        | false ->
          Valid
            { Name = dto.Name.Value
              Email = dto.Email.Value
              UserID = Guid.NewGuid()
              Roles = dto.Roles.Value }
    }

  let save usr : EffectRoute<HttpHandler> =
    effectRoute {
      do! emit { Event = UserCreated usr }
      let! url = buildUrl ["users"] []
      return! renderSuccess url
    }

  effectRoute {
    let! vmr = getModelRoot
    do! requireRole Roles.UserManagement
    let! dto = fromForm<CreateUserDto>
    let! p = getPorts
    let! validationResult = validate p dto vmr.Translate

    return!
      match validationResult with
      | Valid user -> save user
      | Invalid userCreationViewModel ->
        renderOk
          CreateUser.createUserView
          { Model = userCreationViewModel
            Root = vmr }
        |> toEffectRoute
  }

let details id =
  effectRoute {
    let! root = getModelRoot
    do! requireRole Roles.UserManagement
    let! user = fun (p: IPorts) -> p.find<User> id
    return renderOk UserDetails.details { Model = user; Root = root }
  }


let switchRole (userId, role) eventBuilder =
  effectRoute {
    do! requireRole Roles.UserManagement
    let! user = fun (p: IPorts) -> p.find<User> userId

    let! role =
      Enum.GetValues<Roles>()
      |> Seq.tryFind (fun r -> (r |> int) = role)
      |> (function
      | Some r -> Ok r
      | None -> BadRequest |> Error)

    do! emit { Event = (user, role) |> eventBuilder }
    let! url = buildUrl [ "users"; user.ID |> string ] []
    return! renderSuccess url
  }

let addRole userIdRole =
  switchRole userIdRole (fun (u, r) -> RoleAdded { UserID = u.ID; RoleToAdd = r })

let removeRole userIdRole =
  switchRole userIdRole (fun (u, r) -> RoleRemoved { UserID = u.ID; RoleRemoved = r })
