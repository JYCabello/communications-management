﻿module CommunicationsManagement.API.Models

open System

[<CLIMutable>]
type Configuration =
  { EventStoreConnectionString: string
    BaseUrl: string
    AdminEmail: string }

[<CLIMutable>]
type Message = { ID: int; Amount: int }

[<CLIMutable>]
type ToxicEvent = { Content: string; Type: string }

type Roles =
  | None = 1
  | Delegate = 1
  | Press = 2
  | Admin = 131071

type Email = Email of string

let contains (searchTerm: Roles) (userRoles: Roles) = (searchTerm &&& userRoles) = searchTerm

type User =
  { Name: string
    ID: Guid
    Email: Email
    Roles: Roles }
  member this.hasRole roles = contains roles this.Roles

type Session = { ID: Guid; UserID: Guid }

type Translator = string -> string

type ViewModelRoot =
  { User: User option
    Title: string
    Translate: Translator
    CurrentUrl: string
    BaseUrl: string }

type ViewModel<'a> = { Root: ViewModelRoot; Model: 'a }

type StreamEvent =
  | Message of Message
  | Toxic of ToxicEvent

let getEventTypeName =
  function
  | Message _ -> "Message"
  | Toxic _ -> "Toxic"

let getStreamName =
  function
  | Message _ -> "deletable"
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
