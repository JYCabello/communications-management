module CommunicationsManagement.API.Views.Home

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open Flurl

let private usersRow vm =
  vm.Root.User
  |> Option.map (fun u ->
    match u.hasRole Roles.UserManagement with
    | false -> []
    | true ->
      [ a [ _href (
              vm
                .Root
                .BaseUrl
                .AppendPathSegment("users")
                .ToString()
            )
            _class "btn btn-primary"
            _id "users-link" ] [
          "Users" |> vm.Root.Translate |> Text
        ]

        ])
  |> Option.toList
  |> List.collect id

let homeView (vm: ViewModel<unit>) : XmlNode list = [ yield! usersRow vm ]
