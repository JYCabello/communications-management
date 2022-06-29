﻿module CommunicationsManagement.API.Routing.Users

open System
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Views.Users
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Giraffe
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views.Users.ListUsers
open CommunicationsManagement.API.Views.Users.CreateUser
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.DataValidation
open Flurl
open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes

let list (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effectRoute {
    let! root = buildModelRoot
    do! requireRole Roles.UserManagement (root.Translate "Users")
    let! users = fun (p: IPorts) -> p.getAll<User> ()

    return
      { Model = { Users = users }
        Root = root }
  }
  |> resolveER usersListView ports next ctx

let create (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effectRoute {
    let! root = buildModelRoot
    do! requireRole Roles.UserManagement (root.Translate "Users")

    return
      { Model =
          { Name = None
            NameError = None
            Email = None
            EmailError = None
            Roles = Roles.None
            RolesError = None }
        Root = root }
  }
  |> resolveER createUserView ports next ctx

[<CLIMutable>]
type CreateUserDto =
  { Name: string option
    Email: string option
    Roles: Roles option }

type UserCreationValidation =
  | Valid of UserCreated
  | Invalid of UserCreationViewModel

let createPost (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  let validate (p: IPorts) (dto: CreateUserDto) (tr: Translator) : Task<UserCreationValidation> =
    task {
      let emailError = isValidEmail dto.Email tr

      let! emailExists =
        match emailError with
        | Some _ -> false |> Task.FromResult
        | None ->
          match dto.Email with
          | None -> true |> Task.FromResult
          | Some email ->
            p.find<User> (fun u -> u.Email = Email email)
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

  let save usr vmr : Effect<ViewModel<UserCreationViewModel>> =
    effect {
      do! fun p -> p.sendEvent { Event = UserCreated usr }

      return!
        renderOk { Model = "Ok"; Root = vmr } successMessage
        |> EarlyReturn
        |> Error
    }

  effectRoute {
    let! root = buildModelRoot
    do! requireRole Roles.UserManagement (root.Translate "Users") ctx

    let! (dto: CreateUserDto) = bindForm

    let! p = getPorts

    let! validationResult = validate p dto root.Translate

    return!
      match validationResult with
      | Valid user -> save user root
      | Invalid userCreationViewModel ->
        { Model = userCreationViewModel
          Root = root }
        |> Task.FromResult
        |> fromTask
  }
  |> resolveER createUserView ports next ctx

let details
  (id: Guid)
  (ports: IPorts)
  (next: HttpFunc)
  (ctx: HttpContext)
  : Task<HttpContext option> =
  effectRoute {
    let! root = buildModelRoot
    do! requireRole Roles.UserManagement (root.Translate "Users")
    let! user = fun (p: IPorts) -> p.query<User> id
    return { Model = user; Root = root }
  }
  |> resolveER UserDetails.details ports next ctx

let userUrl (p: IPorts) (u: User) =
  p
    .configuration
    .BaseUrl
    .AppendPathSegments("users", u.ID)
    .ToString()

let addRole
  (userId: Guid)
  (role: int)
  (ports: IPorts)
  (next: HttpFunc)
  (ctx: HttpContext)
  : HttpFuncResult =
  effectRoute {
    let! root = buildModelRoot
    do! requireRole Roles.UserManagement (root.Translate "Users")
    let! user = fun (p: IPorts) -> p.query<User> userId

    let! role =
      Enum.GetValues<Roles>()
      |> Seq.tryFind (fun r -> (r |> int) = role)
      |> (function
      | Some r -> Ok r
      | None -> BadRequest |> Error)

    do! fun (p: IPorts) -> p.sendEvent { Event = RoleAdded { UserID = user.ID; RoleToAdd = role } }

    return user
  }
  |> resolveERRedirect userUrl ports next ctx

let removeRole
  (userId: Guid)
  (role: int)
  (ports: IPorts)
  (next: HttpFunc)
  (ctx: HttpContext)
  : HttpFuncResult =
  effectRoute {
    let! root = buildModelRoot
    do! requireRole Roles.UserManagement (root.Translate "Users")
    let! user = fun (p: IPorts) -> p.query<User> userId

    let! role =
      Enum.GetValues<Roles>()
      |> Seq.tryFind (fun r -> (r |> int) = role)
      |> (function
      | Some r -> Ok r
      | None -> BadRequest |> Error)

    do! fun (p: IPorts) -> p.sendEvent { Event = RoleRemoved { UserID = user.ID; RoleRemoved = role } }

    return user
  }
  |> resolveERRedirect userUrl ports next ctx
