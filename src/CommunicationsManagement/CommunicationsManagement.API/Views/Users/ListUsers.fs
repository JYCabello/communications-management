[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Users.ListUsers

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Giraffe.ViewEngine
open Urls

type UserListViewModel = { Users: User list }

let usersListView (vm: ViewModel<UserListViewModel>) : XmlNode list =
  let url (u: User) =
    vm.Root.BaseUrl |> append "users" |> append u.ID

  let userRow (u: User) =
    let email =
      u.Email
      |> function
        | Email e -> e

    div [ _class "row" ] [
      div [ _class "col" ] [ u.Name |> Text ]
      div [ _class "col" ] [ email |> Text ]
      div [ _class "col" ] [
        match u.LastLogin with
        | None -> "Never" |> vm.Root.Translate |> Text
        | Some ll -> ll.ToString("yyyy-MM-dd hh:mm:ss") |> Text
      ]
      div [ _class "col" ] [
        yield!
          match u.Roles with
          | Roles.Admin -> []
          | _ ->
            [ a [ _href <| url u
                  _class "btn btn-info btn-sm"
                  _data "user-details" email ] [
                "Details" |> vm.Root.Translate |> Text
              ] ]
      ]
    ]

  let newUserUrl =
    vm.Root.BaseUrl
    |> append "users"
    |> append "create"

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
          "LastLogin" |> vm.Root.Translate |> Text
        ]
        div [ _class "col" ] [
          "Actions" |> vm.Root.Translate |> Text
        ]
      ]
      yield! vm.Model.Users |> List.map userRow
    ] ]
