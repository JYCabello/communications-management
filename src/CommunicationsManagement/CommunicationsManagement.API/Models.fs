﻿module CommunicationsManagement.API.Models

open System

[<CLIMutable>]
type Configuration =
  { EventStoreConnectionString: string
    BaseUrl: string
    AdminEmail: string
    SendGridKey: string
    MailFrom: string }

[<CLIMutable>]
type ToxicEvent = { Content: string; Type: string }

[<CLIMutable>]
type SessionCreated =
  { SessionID: Guid
    UserID: Guid
    ExpiresAt: DateTime }

[<CLIMutable>]
type SessionTerminated = { SessionID: Guid }

type Roles =
  | None = 0
  | Delegate = 1
  | Press = 2
  | UserManagement = 4
  | Admin = 131071

type Email = Email of string

let contains (searchTerm: Roles) (userRoles: Roles) =
  (searchTerm &&& userRoles) = searchTerm
  && (userRoles = Roles.None |> not)

type User =
  { Name: string
    ID: Guid
    Email: Email
    Roles: Roles }
  member this.hasRole roles = contains roles this.Roles

type Session =
  { ID: Guid
    UserID: Guid
    ExpiresAt: DateTime }

type Translator = string -> string

type ViewModelRoot =
  { User: User option
    Title: string
    Translate: Translator
    CurrentUrl: string
    BaseUrl: string }

type ViewModel<'a> = { Root: ViewModelRoot; Model: 'a }

type StreamEvent =
  | SessionCreated of SessionCreated
  | SessionTerminated of SessionTerminated
  | Toxic of ToxicEvent

let getEventTypeName =
  function
  | SessionCreated _ -> "SessionCreated"
  | SessionTerminated _ -> "SessionTerminated"
  | Toxic _ -> "Toxic"

let getStreamName =
  function
  | SessionCreated _ -> "Sessions"
  | SessionTerminated _ -> "Sessions"
  | Toxic _ -> "toxic"

type DomainError =
  | NotAuthenticated
  | Unauthorized of protectedResourceName: string
  | NotFound of resourceName: string
  | Conflict
  | BadRequest
  | InternalServerError of errorMessage: string
  | EarlyReturn of Giraffe.Core.HttpHandler

type SendEventParams = { Event: StreamEvent }

type WelcomeNotification = { UserName: string }

type LoginNotification =
  { UserName: string
    ActivationCode: Guid
    ActivationUrl: string }

type Notification =
  | Welcome of WelcomeNotification
  | Login of LoginNotification

type SendNotificationParams =
  { Notification: Notification
    Email: Email }
