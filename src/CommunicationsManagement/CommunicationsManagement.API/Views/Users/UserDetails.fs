[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Users.UserDetails

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open Urls

let details (vm: ViewModel<User>) =
  let trxTxt = vm.Root.Translate >> Text
  let m = vm.Model

  let email =
    m.Email
    |> function
      | Email e -> e

  let labelFor i18nTag forId =
    label [ _class "form-label"
            yield!
              forId
              |> Option.map (fun id -> _for id)
              |> Option.toList ] [
      i18nTag |> trxTxt
    ]

  let disabledInputFor value name i18nTag =
    [ labelFor i18nTag (Some $"input-%s{name}")
      div [ _class "input-group mb-3" ] [
        input [ _class "form-control"
                _name name
                _disabled
                _id $"input-%s{name}"
                value |> _value ]
      ] ]

  let roleButton role i18tag =
    let addRoleUrl =
      urlFor
        vm.Root.BaseUrl
        [ "users"
          m.ID
          "roles"
          "add"
          role |> int ]
        []

    let removeRoleUrl =
      urlFor
        vm.Root.BaseUrl
        [ "users"
          m.ID
          "roles"
          "remove"
          role |> int ]
        []

    li [ _class "list-group-item" ] [
      div [] [ i18tag |> trxTxt ]

      match m.hasRole role with
      | true ->
        a [ _href removeRoleUrl
            _id $"remove-role-{role |> int}"
            _class "btn btn-danger btn-sm" ] [
          "Remove" |> trxTxt
        ]
      | false ->
        a [ _href addRoleUrl
            _id $"add-role-{role |> int}"
            _class "btn btn-success btn-sm" ] [
          "Add" |> trxTxt
        ]
    ]

  [ h1 [] [ "Details" |> trxTxt ]
    form [ _novalidate ] [
      yield! disabledInputFor m.Name "name" "Name"
      yield! disabledInputFor email "email" "Email"
      div [ _class "input-group mb-3" ] [
        labelFor "Roles" None
        ul [ _class "list-group list-group-flush" ] [
          roleButton Roles.Press "Press"
          roleButton Roles.Delegate "Delegate"
          roleButton Roles.ChannelManagement "ChannelManagement"
          roleButton Roles.UserManagement "UserManagement"
        ]
      ]
    ] ]
