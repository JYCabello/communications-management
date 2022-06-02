module CommunicationsManagement.API.Operations

open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models

type HandleMessage<'a> = 'a -> Effect<unit>
type GetHandler<'a> = Message -> HandleMessage<'a>
