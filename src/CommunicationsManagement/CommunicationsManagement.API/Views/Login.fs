﻿module CommunicationsManagement.API.Views.Login
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine

  type LoginDto = {
    Email: string option
    EmailError: string option
  }

let loginView (vm: ViewModel<LoginDto>) =
  let emailError =
    vm.Model.EmailError
    |> Option.map (fun e -> div [ _class "invalid-feedback" ] [ Text e ])
    |> Option.toList

  let emailValidClass =
    vm.Model.EmailError
    |> Option.map (fun _ -> " is-invalid")
    |> Option.defaultValue ""

  [ form [ _action "/login"; _method "post"; _novalidate ] [
    label [_class "form-label"; _for "input-email"] [ Text "Email" ]
    div [_class "input-group mb-3"] [
      input [ _class $"form-control{emailValidClass}"; _name "email"; _id "input-email" ]
      yield! emailError
    ]
    input [ _type "submit"; _id "email-sumbit"; _class "btn btn-primary" ]
  ] ]
