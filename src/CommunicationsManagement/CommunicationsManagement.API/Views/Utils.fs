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

let labelFor trx i18nTag forId =
  label [ _class "form-label"
          yield!
            forId
            |> Option.map (fun id -> _for id)
            |> Option.toList ] [
    i18nTag |> trx |> Text
  ]

let inputGroupFor trx value error name i18nTag =
  [ labelFor trx i18nTag (Some $"input-%s{name}")
    div [ _class "input-group mb-3" ] [
      input [ _class $"form-control {validationClass error}"
              _name name
              _id $"input-%s{name}"
              value |> Option.defaultValue "" |> _value ]

      yield! validationError error
    ] ]
