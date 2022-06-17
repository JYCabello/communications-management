module CommunicationsManagement.API.Views.Users

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Giraffe.ViewEngine
open Flurl

type UserListViewModel = { Users: User list }



let usersListView (vm: ViewModel<UserListViewModel>) : XmlNode list =
  let userRow (u: User) =
    div [ _class "row" ] [
        div [ _class "col" ] [
          u.Name |> Text
        ]
        div [ _class "col" ] [
          u.Email |> function | Email e -> e |> Text
        ]
        div [ _class "col" ] [
          a [ _href (
                vm
                  .Root
                  .BaseUrl
                  .AppendPathSegment("users")
                  .AppendPathSegment(u.ID.ToString())
                  .ToString()
              )
              _class "btn btn-outline-primary btn-sm user-link"
               ] [
            "Details" |> vm.Root.Translate |> Text
          ]
        ]
      ]
  
  [ div [ _class "container" ] [
      div [ _class "row bd-highlight" ] [
        div [ _class "col" ] [
          "User" |> vm.Root.Translate |> Text
        ]
        div [ _class "col" ] [
          "Email" |> vm.Root.Translate |> Text
        ]
        div [ _class "col" ] [
          "Actions" |> vm.Root.Translate |> Text
        ]
      ]
      yield! vm.Model.Users |> List.map userRow
    ] ]
