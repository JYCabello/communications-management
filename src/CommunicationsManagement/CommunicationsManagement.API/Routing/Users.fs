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
            Roles = Roles.Press
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

    return
      { Model =
          { Name = None
            NameError = None
            Email = None
            EmailError = None
            Roles = dto.Roles |> Option.defaultValue Roles.None
            RolesError = None }
        Root = root }
  }
  |> resolveEffect2 ports createUserView next ctx
