[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Notifications

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling
open SendGrid
open SendGrid.Helpers.Mail

let send (c: Configuration) (p: SendNotificationParams) : Task<Result<unit, DomainError>> =
  taskResult {
    let key = c.SendGridKey
    let client = SendGridClient(key)
    let message = SendGridMessage()
    message.SetFrom(c.MailFrom)

    message.AddTo(
      p.Email
      |> function
        | Email s -> s
    )

    match p.Notification with
    | Login loginNotification ->
      message.SetSubject("Login attempt")
      message.AddContent(MimeType.Html, loginNotification.ActivationUrl)
    | Welcome welcomeNotification ->
      message.SetSubject("Welcome")
      message.AddContent(MimeType.Text, $"Welcome, {welcomeNotification.UserName}")

    do! client.SendEmailAsync(message) :> Task
  }
