module CommunicationsManagement.API.Models

[<CLIMutable>]
type Configuration = { EventStoreConnectionString: string }

[<CLIMutable>]
type Message = { ID: int; Amount: int }

type StreamEvent =
  | Message of Message
  | Toxic of eventType: string * content: string
