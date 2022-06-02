module CommunicationsManagement.API.Models

open System.Threading
open System.Threading.Tasks
open EventStore.Client

[<CLIMutable>]
type Configuration = { EventStoreConnectionString: string }

[<CLIMutable>]
type Message = { ID: int; Amount: int }

[<CLIMutable>]
type ToxicEvent = { Content: string; Type: string }

type Roles =
  | Admin = 1
  | Delegate = 2
  | Press = 4

let contains (searchTerm: Roles) (roles: Roles) = (searchTerm &&& roles) = searchTerm

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
  | Unauthorized of protectedResourceName: string
  | NotFound of resourceName: string
  | Conflict
  | InternalServerError of errorMessage: string

type SubscriptionDetails =
  { StreamID: string
    Handler: StreamSubscription -> ResolvedEvent -> CancellationToken -> Task }

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
