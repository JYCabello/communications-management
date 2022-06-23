module CommunicationsManagement.API.Views.Users.CreateUser

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
  div [] [
        Text lbl
        input [ _type "checkbox"
                _name "roles"
                if userRoles |> contains role then
                  _checked
                role |> int |> string |> _value ]
      ]

let createUserView (vm: ViewModel<UserCreationViewModel>) =
  [ form [ _action "/users/create"
           _method "post"
           _novalidate ] [
      roleCheckBox vm.Model.Roles Roles.Press "Press"
      roleCheckBox vm.Model.Roles Roles.Delegate "Delegate"
      roleCheckBox vm.Model.Roles Roles.UserManagement "UserManagement"
      input [ _type "submit"
              _id "email-sumbit"
              _class "btn btn-primary" ]
    ] ]
