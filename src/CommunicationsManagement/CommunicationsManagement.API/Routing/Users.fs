module CommunicationsManagement.API.Routing.Users

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

let createPost (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
  effect {
    let! user = auth ctx
    let! root = buildModelRoot user ctx
    do! requireRole user Roles.UserManagement (root.Translate "Users")

    let! dto =
      ctx.TryBindFormAsync<CreateUserDto>()
      |> TaskResult.mapError (fun _ -> BadRequest)

    let nameError =
      match System.String.IsNullOrWhiteSpace(dto.Name |> Option.defaultValue "") with
      | true -> "CannotBeEmpty" |> root.Translate |> Some
      | false -> None

    return
      { Model =
          { Name = dto.Name
            NameError = nameError
            Email = dto.Email
            EmailError = isValidEmail dto.Email root.Translate
            Roles = dto.Roles |> Option.defaultValue Roles.None
            RolesError =
              match dto.Roles with
              | None -> "MustSelectOneOption" |> root.Translate |> Some
              | _ -> None }
        Root = root }
  }
  |> resolveEffect2 ports createUserView next ctx
