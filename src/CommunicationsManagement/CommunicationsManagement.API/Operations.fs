module CommunicationsManagement.API.Operations

open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models

type HandleMessage<'a> = 'a -> IPorts -> Task<Result<unit, DomainError>>
type GetHandler<'a> = Message -> HandleMessage<'a>
