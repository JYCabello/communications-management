module CommunicationsManagement.API.DataValidation

open System.Net.Mail
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Models

let validateEmail fieldName (email: string option) : ValidateResult<Email> =
  let validateFurther te =
    try
        let a = MailAddress(te)

        match a.Address = te with
        | true -> te |> Email |> Validate.valid
        | false -> Validate.validationError fieldName "InvalidEmail"
      with
      | _ -> Validate.validationError fieldName "InvalidEmail"

  match email with
  | None -> Validate.validationError fieldName "CannotBeEmpty"
  | Some e ->
    let trimmedEmail = e.Trim()

    match trimmedEmail.EndsWith(".") with
    | true -> Validate.validationError fieldName "InvalidEmail"
    | false -> validateFurther trimmedEmail
