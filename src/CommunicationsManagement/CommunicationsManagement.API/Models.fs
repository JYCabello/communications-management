module CommunicationsManagement.API.Models

open System
open System.Threading
open System.Threading.Tasks
open EventStore.Client

[<CLIMutable>]
type Configuration = { EventStoreConnectionString: string }

[<CLIMutable>]
type Message = { ID: int; Amount: int }

type StreamEvent =
  | Message of Message
  | Toxic of eventType: string * content: string

type DomainError =
  | Unauthorized of protectedResourceName: string
  | NotFound of resourceName: string
  | Conflict
  | InternalServerError of errorMessage: string

type SubscriptionDetails =
  { StreamID: string
    Handler: StreamSubscription -> ResolvedEvent -> CancellationToken -> Task }
