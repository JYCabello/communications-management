[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Home

open CommunicationsManagement.API.Models
open CommunicationsManagement.API
open Giraffe.ViewEngine
open Flurl


let private usersRow vm =
  let url =
    vm
      .Root
      .BaseUrl
      .AppendPathSegment("users")
      .ToString()

  vm.Root.User
  |> Option.bindBool (fun u -> u.hasRole Roles.UserManagement)
  |> Option.map (fun _ ->
    [ a [ _href url
          _class "btn btn-primary"
          _id "users-link" ] [
        "Users" |> vm.Root.Translate |> Text
      ] ])
  |> Option.defaultValue []

let homeView (vm: ViewModel<unit>) : XmlNode list = [ yield! usersRow vm ]
