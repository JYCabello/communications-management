namespace CommunicationsManagement.API.Views
module LoginModels =
  type LoginDto = {
    Email: string option
    EmailError: string option
  }

[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module Login =

  open CommunicationsManagement.API.Models
  open Giraffe.ViewEngine
  open LoginModels

  let view (vm: ViewModel<LoginDto>) =
    let emailError =
      vm.Model.EmailError
      |> Option.map (fun e -> div [ _class "invalid-feedback" ] [ Text e ])
      |> Option.toList

    let emailValidClass =
      vm.Model.EmailError
      |> Option.map (fun _ -> " is-invalid")
      |> Option.defaultValue ""

    [ form [ _action "/login"; _method "post"; _novalidate ] [
      div [_class "input-group mb-3"] [
        label [_class "form-label"; _for "input-email"] [ Text "Email" ]
        input [ _class $"form-control{emailValidClass}"; _name "email"; _id "input-email" ]
        yield! emailError
      ]
      input [ _type "submit"; _id "email-sumbit"; _class "btn btn-primary" ]
    ] ]
