[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Notifications

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.NotificationModels
open FsToolkit.ErrorHandling
open SendGrid
open SendGrid.Helpers.Mail
open System

let send
  (c: Configuration)
  (p: SendNotificationParams)
  (tr: Translator)
  : Task<Result<unit, DomainError>> =
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

    do
      match p.Notification with
      | Login loginNotification ->
        message.SetSubject(tr "LoginAttemptSubject")
        message.AddContent(MimeType.Html, loginNotification.ActivationUrl)
      | Welcome welcomeNotification ->
        message.SetSubject(tr "WelcomeSubject")

        message.AddContent(
          MimeType.Text,
          "WelcomeEmailBodyTemplate"
          |> tr
          |> fun s -> String.Format(s, welcomeNotification.UserName)
        )

    do! client.SendEmailAsync(message) :> Task
  }
