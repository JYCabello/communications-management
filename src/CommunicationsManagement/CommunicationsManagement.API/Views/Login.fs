module CommunicationsManagement.API.Views.Login

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open Utils

type LoginModel =
  { Email: string option
    EmailError: string option }

let loginView (vm: ViewModel<LoginModel>) =
  [ form [ _action "/login"
           _method "post"
           _novalidate ] [
      label [ _class "form-label"
              _for "input-email" ] [
        Text(vm.Root.Translate "Email")
      ]
      div [ _class "input-group mb-3" ] [
        input [ _class $"form-control{validationClass vm.Model.EmailError}"
                _name "email"
                _id "input-email"
                vm.Model.Email |> Option.defaultValue "" |> _value ]

        yield! validationError vm.Model.EmailError
      ]
      input [ _type "submit"
              _id "email-sumbit"
              _class "btn btn-primary" ]
    ] ]

let loginMessage vm =
  [ Text vm.Model
    Layout.homeButton vm.Root.Translate ]
