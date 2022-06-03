module CommunicationsManagement.API.Models

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
  | Delegate = 1
  | Press = 2
  | Admin = 131071

let contains (searchTerm: Roles) (userRoles: Roles) = (searchTerm &&& userRoles) = searchTerm

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
  | InternalServerError of errorMessage: string

type SendEventParams = { Event: StreamEvent }

type WelcomeNotification = { UserName: string }

type LoginNotification =
  { UserName: string
    ActivationCode: string
    ActivationUrl: string }

type Notification =
  | Welcome of WelcomeNotification
  | Login of LoginNotification

type Email = Email of string

type SendNotificationParams =
  { Notification: Notification
    Email: Email }
