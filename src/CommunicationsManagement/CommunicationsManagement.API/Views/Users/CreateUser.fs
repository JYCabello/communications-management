module CommunicationsManagement.API.Views.Users.CreateUser

open System
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine

type UserCreationViewModel =
  { Name: string option
    NameError: string option
    Email: string option
    EmailError: string option
    Roles: Roles
    RolesError: string option }

let roleCheckBox userRoles role lbl =
  let inputID = Guid.NewGuid().ToString()

  li [ _class "list-group-item" ] [
    div [ _class "form-check form-switch" ] [
      input [ _type "checkbox"
              _name "roles"
              _class "form-check-input"
              _id inputID
              if userRoles |> contains role then
                _checked
              role |> int |> string |> _value ]
      label [ _class "form-check-label"
              _for inputID ] [
        Text lbl
      ]
    ]
  ]

let createUserView (vm: ViewModel<UserCreationViewModel>) =
  let trx = vm.Root.Translate

  [ form [ _action "/users/create"
           _method "post"
           _novalidate ] [
      p [ _class "bold" ] [
        "Roles" |> trx |> Text
      ]
      ul [ _class "list-group list-group-flush" ] [
        roleCheckBox vm.Model.Roles Roles.Press (trx "Press")
        roleCheckBox vm.Model.Roles Roles.Delegate (trx "Delegate")
        roleCheckBox vm.Model.Roles Roles.UserManagement (trx "UserManagement")
      ]
      input [ _type "submit"
              _id "email-sumbit"
              _class "btn btn-primary" ]
    ] ]
