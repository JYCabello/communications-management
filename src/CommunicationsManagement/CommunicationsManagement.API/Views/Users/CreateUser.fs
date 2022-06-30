[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Users.CreateUser

open System
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views
open FsToolkit.ErrorHandling
open Giraffe.ViewEngine
open CommunicationsManagement.API.Views.Utils

type UserCreationViewModel =
  { Name: string option
    NameError: string option
    Email: string option
    EmailError: string option
    Roles: Roles
    RolesError: string option }


let createUserView (vm: ViewModel<UserCreationViewModel>) =
  let trx = vm.Root.Translate
  let m = vm.Model

  let labelFor i18nTag forId =
    label [ _class "form-label"
            yield!
              forId
              |> Option.map (fun id -> _for id)
              |> Option.toList ] [
      i18nTag |> trx |> Text
    ]

  let inputGroupFor value error name i18nTag =
    [ labelFor i18nTag (Some $"input-%s{name}")
      div [ _class "input-group mb-3" ] [
        input [ _class $"form-control {validationClass error}"
                _name name
                _id $"input-%s{name}"
                value |> Option.defaultValue "" |> _value ]

        yield! validationError error
      ] ]

  let roleCheckBox role lbl =
    let inputID = Guid.NewGuid().ToString()

    li [ _class "list-group-item" ] [
      div [ _class "form-check form-switch" ] [
        input [ _type "checkbox"
                _name "roles"
                _class $"form-check-input %s{validationClass m.RolesError}"
                _id inputID
                if m.Roles |> contains role then
                  _checked
                role |> int |> string |> _value ]
        label [ _class "form-check-label"
                _for inputID ] [
          Text lbl
        ]
        yield! validationError m.RolesError
      ]
    ]

  [ h1 [] [ "CreateUser" |> trx |> Text ]
    form [ _action "/users/create"
           _method "post"
           _novalidate ] [
      yield! inputGroupFor m.Name m.NameError "name" "Name"
      yield! inputGroupFor m.Email m.EmailError "email" "Email"
      div [ _class "input-group mb-3" ] [
        labelFor "Roles" None
        ul [ _class "list-group list-group-flush" ] [
          roleCheckBox Roles.Press (trx "Press")
          roleCheckBox Roles.Delegate (trx "Delegate")
          roleCheckBox Roles.ChannelManagement (trx "ChannelManagement")
          roleCheckBox Roles.UserManagement (trx "UserManagement")
        ]
      ]
      input [ _type "submit"
              _id "create-user-sumbit"
              _class "btn btn-primary" ]
    ] ]

let successMessage vm =
  [ Text vm.Model
    Layout.homeButton vm.Root.Translate ]
