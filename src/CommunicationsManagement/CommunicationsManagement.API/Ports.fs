module CommunicationsManagement.API.Ports

open System
open System.Threading.Tasks
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Models.NotificationModels

type IPorts =
  abstract member sendEvent: SendEventParams -> Task<Result<unit, DomainError>>

  abstract member sendNotification:
    Translator -> SendNotificationParams -> Task<Result<unit, DomainError>>

  abstract member configuration: Configuration
  abstract member find<'a> : Guid -> Task<Result<'a, DomainError>>
  abstract member query<'a> : ('a -> bool) -> Task<Result<'a, DomainError>>
  abstract member save<'a> : 'a -> Task<Result<unit, DomainError>>
  abstract member delete<'a> : Guid -> Task<Result<unit, DomainError>>
  abstract member getAll<'a> : unit -> Task<Result<'a list, DomainError>>
