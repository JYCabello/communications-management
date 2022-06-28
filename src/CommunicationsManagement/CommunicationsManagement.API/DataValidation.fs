module CommunicationsManagement.API.DataValidation

open System.Net.Mail
open CommunicationsManagement.API.Models

let isValidEmail (email: string option) (tr: Translator) : string option =
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
