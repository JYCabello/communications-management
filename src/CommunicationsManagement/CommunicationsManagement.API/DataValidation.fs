module CommunicationsManagement.API.DataValidation

open System.Net.Mail
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Models

let validateEmail (email: string option) (tr: Translator) : string option =
  let errorMessage = "InvalidEmail" |> tr |> Some

  match email with
  | None -> errorMessage
  | Some e ->
    let trimmedEmail = e.Trim()

    match trimmedEmail.EndsWith(".") with
    | true -> errorMessage
    | false ->
      try
        let a = MailAddress(e)

        match a.Address = trimmedEmail with
        | true -> None
        | false -> errorMessage
      with
      | _ -> errorMessage

let validateEmail2 fieldName (email: string option) : ValidateResult<string> =
  match email with
  | None -> Validate.validationError fieldName "CannotBeEmpty"
  | Some e ->
    let trimmedEmail = e.Trim()

    match trimmedEmail.EndsWith(".") with
    | true -> Validate.validationError fieldName "InvalidEmail"
    | false ->
      try
        let a = MailAddress(e)

        match a.Address = trimmedEmail with
        | true -> Validate.valid e
        | false -> Validate.validationError fieldName "InvalidEmail"
      with
      | _ -> Validate.validationError fieldName "InvalidEmail"
