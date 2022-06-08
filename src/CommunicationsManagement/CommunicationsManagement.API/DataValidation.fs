module CommunicationsManagement.API.DataValidation

open System.Net.Mail

let isValidEmail (email : string option) : bool =
  match email with
  | None -> false
  | Some e ->
    let trimmedEmail = e.Trim()
    match trimmedEmail.EndsWith(".") with
    | true -> false
    | false ->
      try
        let a = MailAddress(e)
        a.Address = trimmedEmail 
      with
      | _ -> false
