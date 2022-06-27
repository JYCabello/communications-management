module CommunicationsManagement.API.Views.Users.ListUsers

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Giraffe.ViewEngine
open Flurl

type UserListViewModel = { Users: User list }

let usersListView (vm: ViewModel<UserListViewModel>) : XmlNode list =
  let url (u: User) =
    vm
      .Root
      .BaseUrl
      .AppendPathSegments("users", u.ID.ToString())
      .ToString()

  let userRow (u: User) =
    let email =
      u.Email
      |> function
        | Email e -> e

    div [ _class "row" ] [
      div [ _class "col" ] [ u.Name |> Text ]
      div [ _class "col" ] [ email |> Text ]
      div [ _class "col" ] [
        a [ _href <| url u
            _class "btn btn-info btn-sm"
            _data "user-details" email ] [
          "Details" |> vm.Root.Translate |> Text
        ]
      ]
    ]

  let newUserUrl =
    vm
      .Root
      .BaseUrl
      .AppendPathSegments("users", "create")
      .ToString()

  [ div [ _class "d-flex flex-row-reverse" ] [
      a [ _href newUserUrl
          _class "btn btn-success btn-sm user-link"
          _id "new-user-button" ] [
        "New" |> vm.Root.Translate |> Text
      ]
    ]
    div [ _class "container" ] [
      div [ _class "row p-2 bd-highlight" ] [
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
