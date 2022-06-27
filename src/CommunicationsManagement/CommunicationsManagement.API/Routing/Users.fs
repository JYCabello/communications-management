module CommunicationsManagement.API.Routing.Users

open System
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http
open Giraffe
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views.Users.ListUsers
open CommunicationsManagement.API.Views.Users.CreateUser
open CommunicationsManagement.API.Routing.Routes.Rendering
open CommunicationsManagement.API.DataValidation

let list (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! user = auth ctx
    let! root = buildModelRoot user ctx
    do! requireRole user Roles.UserManagement (root.Translate "Users")
    let! users = fun p -> p.getAll<User> ()

    return
      { Model = { Users = users }
        Root = root }
  }
  |> resolveEffect2 ports usersListView next ctx

let create (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! user = auth ctx
    let! root = buildModelRoot user ctx
    do! requireRole user Roles.UserManagement (root.Translate "Users")

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
  |> resolveEffect2 ports createUserView next ctx

[<CLIMutable>]
type CreateUserDto =
  { Name: string option
    Email: string option
    Roles: Roles option }

type UserCreationValidation =
  | Valid of UserCreated
  | Invalid of UserCreationViewModel

let createPost (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  let validate (dto: CreateUserDto) (tr: Translator) : Task<UserCreationValidation> =
    task {
      let emailError = isValidEmail dto.Email tr

      let! emailExists =
        match emailError with
        | Some _ -> false |> Task.FromResult
        | None ->
          match dto.Email with
          | None -> true |> Task.FromResult
          | Some email ->
            ports.find<User> (fun u -> u.Email = Email email)
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

  effect {
    let! user = auth ctx
    let! root = buildModelRoot user ctx
    do! requireRole user Roles.UserManagement (root.Translate "Users")

    let! dto =
      ctx.TryBindFormAsync<CreateUserDto>()
      |> TaskResult.mapError (fun _ -> BadRequest)

    let! validationResult = validate dto root.Translate

    return!
      match validationResult with
      | Valid user -> save user root
      | Invalid userCreationViewModel ->
        { Model = userCreationViewModel
          Root = root }
        |> Task.FromResult
        |> fromTask
  }
  |> resolveEffect2 ports createUserView next ctx

let details (id: Guid) (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  failwith "not implemented"
