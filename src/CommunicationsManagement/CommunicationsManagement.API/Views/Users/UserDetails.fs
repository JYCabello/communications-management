module CommunicationsManagement.API.Views.Users.UserDetails

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open CommunicationsManagement.API.Views.Utils

let details (vm: ViewModel<User>) =
    let trx = vm.Root.Translate
    let m = vm.Model
    let email = m.Email |> function Email e -> e

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
      
    let roleLink role i18tag =
      li [ _class "list-group-item" ] [
        match m.hasRole role with
        | true -> "Has it" |> Text
        | false -> "Does not have it" |> Text
      ]
    
    [ h1 [] [ "Details" |> trx |> Text ]
      form [ _novalidate ] [
        yield! disabledInputFor m.Name "name" "Name"
        yield! disabledInputFor email "email" "Email"
        div [ _class "input-group mb-3" ] [
          labelFor "Roles" None
          ul [ _class "list-group list-group-flush" ] [
            roleLink Roles.Press "Press"
            roleLink Roles.Delegate "Delegate"
            roleLink Roles.UserManagement "UserManagement"
          ]
        ]
      ] ]
