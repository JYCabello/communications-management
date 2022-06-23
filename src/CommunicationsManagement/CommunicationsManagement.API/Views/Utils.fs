module CommunicationsManagement.API.Views.Utils

open Giraffe.ViewEngine

let validationError =
  function
  | None -> []
  | Some e ->
    [ div [ _class "invalid-feedback" ] [
        Text e
      ] ]

let validationClass =
  function
  | None -> ""
  | Some _ -> " is-invalid"
