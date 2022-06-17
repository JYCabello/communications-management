module CommunicationsManagement.API.Routing.Users

open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open Microsoft.AspNetCore.Http
open Giraffe
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views.Users
open CommunicationsManagement.API.Routing.Routes.Rendering


let userList (ports: IPorts) (next: HttpFunc) (ctx: HttpContext) : Task<HttpContext option> =
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
