[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Users.ListUsers

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Giraffe.ViewEngine
open Urls

type UserListViewModel = { Users: User list }

let usersListView (vm: ViewModel<UserListViewModel>) : XmlNode list =
  let trxTxt = vm.Root.Translate >> Text

  let url (u: RegularUser) =
    urlFor vm.Root.BaseUrl [ "users"; u.ID |> string ] []

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
        | None -> "Never" |> trxTxt
        | Some ll -> ll.ToString("yyyy-MM-dd hh:mm:ss") |> Text
      ]
      div [ _class "col" ] [
        yield!
          match u with
          | Admin _ -> []
          | Regular ru ->
            [ a [ _href <| url ru
                  _class "btn btn-info btn-sm"
                  _data "user-details" email ] [
                "Details" |> trxTxt
              ] ]
      ]
    ]

  [ div [ _class "d-flex flex-row-reverse" ] [
      a [ _href
          <| urlFor vm.Root.BaseUrl [ "users"; "create" ] []
          _class "btn btn-success btn-sm user-link"
          _id "new-user-button" ] [
        "New" |> trxTxt
      ]
    ]
    div [ _class "container" ] [
      div [ _class "row p-2 bd-highlight" ] [
        div [ _class "col" ] [
          "User" |> trxTxt
        ]
        div [ _class "col" ] [
          "Email" |> trxTxt
        ]
        div [ _class "col" ] [
          "LastLogin" |> trxTxt
        ]
        div [ _class "col" ] [
          "Actions" |> trxTxt
        ]
      ]
      yield! vm.Model.Users |> List.map userRow
    ] ]
