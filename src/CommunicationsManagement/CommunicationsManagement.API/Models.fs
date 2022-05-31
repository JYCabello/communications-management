module CommunicationsManagement.API.Models

[<CLIMutable>]
type Configuration = { EventStoreConnectionString: string }

[<CLIMutable>]
type Message = { ID: int; Amount: int }
