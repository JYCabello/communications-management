module CommunicationsManagement.API.Views.Users.UserDetails

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open Flurl

let details (vm: ViewModel<User>) =
  let trx = vm.Root.Translate
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
      i18nTag |> trx |> Text
    ]

  let disabledInputFor value name i18nTag =
    [ labelFor i18nTag (Some $"input-%s{name}")
      div [ _class "input-group mb-3" ] [
        input [ _class $"form-control"
                _name name
                _disabled
                _id $"input-%s{name}"
                value |> _value ]
      ] ]

  let roleButton role i18tag =
    let addRoleUrl =
      vm
        .Root
        .BaseUrl
        .AppendPathSegments("users", m.ID, "roles", "add", role |> int |> string)
        .ToString()

    let removeRoleUrl =
      vm
        .Root
        .BaseUrl
        .AppendPathSegments("users", m.ID, "roles", "remove", role |> int |> string)
        .ToString()

    li [ _class "list-group-item" ] [
      div [] [ i18tag |> trx |> Text ]

      match m.hasRole role with
      | true ->
        a [ _href removeRoleUrl
            _id $"remove-role-{role |> int}"
            _class "btn btn-danger btn-sm" ] [
          "Remove" |> trx |> Text
        ]
      | false ->
        a [ _href addRoleUrl
            _id $"add-role-{role |> int}"
            _class "btn btn-success btn-sm" ] [
          "Add" |> trx |> Text
        ]
    ]

  [ h1 [] [ "Details" |> trx |> Text ]
    form [ _novalidate ] [
      yield! disabledInputFor m.Name "name" "Name"
      yield! disabledInputFor email "email" "Email"
      div [ _class "input-group mb-3" ] [
        labelFor "Roles" None
        ul [ _class "list-group list-group-flush" ] [
          roleButton Roles.Press "Press"
          roleButton Roles.Delegate "Delegate"
          roleButton Roles.UserManagement "UserManagement"
        ]
      ]
    ] ]
