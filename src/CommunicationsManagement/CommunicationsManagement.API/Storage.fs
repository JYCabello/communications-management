module CommunicationsManagement.API.Storage

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling


let tryGet a (dict: ConcurrentDictionary<'a, 'b>) : 'b option =
  if dict.ContainsKey a then
    Some dict[a]
  else
    None

[<RequireQualifiedAccess>]
module Sessions =
  let query storage id = storage |> tryGet id |> Task.FromResult

  let getAll (storage: ConcurrentDictionary<Guid, Session>) =
    storage.Values |> Seq.toList |> TaskResult.ok

  let save (storage: ConcurrentDictionary<Guid, Session>) (s: Session) =
    Task.FromResult <| storage[s.ID] <- s

  let find (storage: ConcurrentDictionary<Guid, Session>) q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.FromResult

  let delete (storage: ConcurrentDictionary<Guid, Session>) (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

[<RequireQualifiedAccess>]
module Users =
  let query storage id = storage |> tryGet id |> Task.singleton

  let getAll (storage: ConcurrentDictionary<Guid, User>) =
    storage.Values
    |> Seq.sortByDescending (fun u ->
      match u.LastLogin with
      | None -> DateTime.MinValue
      | Some ll -> ll)
    |> Seq.toList
    |> TaskResult.ok

  let find (storage: ConcurrentDictionary<Guid, User>) q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save (storage: ConcurrentDictionary<Guid, User>) (u: User) =
    Task.singleton <| storage[u.ID] <- u

  let delete (storage: ConcurrentDictionary<Guid, User>) (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

[<RequireQualifiedAccess>]
module Channels =
  let query storage id = storage |> tryGet id |> Task.singleton

  let getAll (storage: ConcurrentDictionary<Guid, Channel>) =
    storage.Values
    |> Seq.sortBy (fun c -> c.IsEnabled)
    |> Seq.toList
    |> TaskResult.ok

  let find (storage: ConcurrentDictionary<Guid, Channel>) q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save (storage: ConcurrentDictionary<Guid, Channel>) (c: Channel) =
    Task.singleton <| storage[c.ID] <- c

  let delete (storage: ConcurrentDictionary<Guid, Channel>) (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

[<RequireQualifiedAccess>]
module EditingCommunicationsRequests =
  let query storage id = storage |> tryGet id |> Task.singleton

  let getAll (storage: ConcurrentDictionary<Guid, EditingCommunicationsRequest>) =
    storage.Values |> Seq.toList |> TaskResult.ok

  let find (storage: ConcurrentDictionary<Guid, EditingCommunicationsRequest>) q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save
    (storage: ConcurrentDictionary<Guid, EditingCommunicationsRequest>)
    (c: EditingCommunicationsRequest)
    =
    Task.singleton <| storage[c.ID] <- c

  let delete (storage: ConcurrentDictionary<Guid, EditingCommunicationsRequest>) (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

let optionToObjResult<'a> (topt: Task<'a option>) =
  topt
  |> Task.map (function
    | Some v -> v |> box |> Ok
    | None -> NotFound typeof<'a>.Name |> Error)

let query<'a> : MemoryStorage -> Configuration -> Guid -> Task<Result<'a, DomainError>> =
  fun s _ id ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> ->
          Sessions.query s.Sessions id
          |> optionToObjResult<Session>
        | t when t = typeof<User> -> Users.query s.Users id |> optionToObjResult<User>
        | t when t = typeof<Channel> ->
          Channels.query s.Channels id
          |> optionToObjResult<Channel>
        | t when t = typeof<EditingCommunicationsRequest> ->
          EditingCommunicationsRequests.query s.EditingCommunicationsRequests id
          |> optionToObjResult<EditingCommunicationsRequest>
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return value :?> 'a
    }

let getAll<'a> : MemoryStorage -> Configuration -> unit -> Task<Result<'a list, DomainError>> =
  fun s _ () ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> -> Sessions.getAll s.Sessions |> TaskResult.map box
        | t when t = typeof<User> -> Users.getAll s.Users |> TaskResult.map box
        | t when t = typeof<Channel> -> Channels.getAll s.Channels |> TaskResult.map box
        | t when t = typeof<EditingCommunicationsRequest> ->
          EditingCommunicationsRequests.getAll s.EditingCommunicationsRequests
          |> TaskResult.map box
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return value :?> 'a list
    }

let queryPredicate<'a> : MemoryStorage
  -> Configuration
  -> ('a -> bool)
  -> Task<Result<'a, DomainError>> =
  fun s _ predicate ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> ->
          box predicate :?> Session -> bool
          |> Sessions.find s.Sessions
          |> optionToObjResult<Session>
        | t when t = typeof<User> ->
          box predicate :?> User -> bool
          |> Users.find s.Users
          |> optionToObjResult<User>
        | t when t = typeof<Channel> ->
          box predicate :?> Channel -> bool
          |> Channels.find s.Channels
          |> optionToObjResult<Channel>
        | t when t = typeof<EditingCommunicationsRequest> ->
          box predicate :?> EditingCommunicationsRequest -> bool
          |> EditingCommunicationsRequests.find s.EditingCommunicationsRequests
          |> optionToObjResult<EditingCommunicationsRequest>
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return value :?> 'a
    }

let save<'a> : MemoryStorage -> Configuration -> 'a -> Task<Result<unit, DomainError>> =
  fun s _ a ->
    taskResult {
      do!
        match typeof<'a> with
        | t when t = typeof<Session> ->
          box a :?> Session
          |> Sessions.save s.Sessions
          |> Task.map Ok
        | t when t = typeof<User> ->
          box a :?> User
          |> Users.save s.Users
          |> Task.map Ok
        | t when t = typeof<Channel> ->
          box a :?> Channel
          |> Channels.save s.Channels
          |> Task.map Ok
        | t when t = typeof<EditingCommunicationsRequest> ->
          box a :?> EditingCommunicationsRequest
          |> EditingCommunicationsRequests.save s.EditingCommunicationsRequests
          |> Task.map Ok
        | t ->
          InternalServerError $"Save not implemented for type {t.FullName}"
          |> TaskResult.error
    }

let delete<'a> : MemoryStorage -> Configuration -> Guid -> Task<Result<unit, DomainError>> =
  fun s _ id ->
    taskResult {
      do!
        match typeof<'a> with
        | t when t = typeof<Session> -> Sessions.delete s.Sessions id |> Task.map Ok
        | t when t = typeof<User> -> Users.delete s.Users id |> Task.map Ok
        | t when t = typeof<Channel> -> Channels.delete s.Channels id |> Task.map Ok
        | t when t = typeof<EditingCommunicationsRequest> ->
          EditingCommunicationsRequests.delete s.EditingCommunicationsRequests id
          |> Task.map Ok
        | t ->
          InternalServerError $"Delete not implemented for type {t.FullName}"
          |> TaskResult.error
    }
