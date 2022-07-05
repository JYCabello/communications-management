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
  let private storage = ConcurrentDictionary<Guid, Session>()

  let query id = storage |> tryGet id |> Task.FromResult

  let getAll () =
    storage.Values |> Seq.toList |> TaskResult.ok

  let save (s: Session) = Task.FromResult <| storage[s.ID] <- s

  let find q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.FromResult

  let delete (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

[<RequireQualifiedAccess>]
module Users =
  let private storage = ConcurrentDictionary<Guid, User>()

  let query id = storage |> tryGet id |> Task.singleton

  let getAll () =
    storage.Values
    |> Seq.sortByDescending (fun u ->
      match u.LastLogin with
      | None -> DateTime.MinValue
      | Some ll -> ll)
    |> Seq.toList
    |> TaskResult.ok

  let find q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save (u: User) = Task.singleton <| storage[u.ID] <- u

  let delete (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

[<RequireQualifiedAccess>]
module Channels =
  let private storage = ConcurrentDictionary<Guid, Channel>()

  let query id = storage |> tryGet id |> Task.singleton

  let getAll () =
    storage.Values
    |> Seq.sortBy (fun c -> c.IsEnabled)
    |> Seq.toList
    |> TaskResult.ok

  let find q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save (c: Channel) = Task.singleton <| storage[c.ID] <- c

  let delete (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

[<RequireQualifiedAccess>]
module EditingCommunicationsRequests =
  let private storage = ConcurrentDictionary<Guid, EditingCommunicationsRequest>()

  let query id = storage |> tryGet id |> Task.singleton

  let getAll () =
    storage.Values |> Seq.toList |> TaskResult.ok

  let find q =
    storage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save (c: EditingCommunicationsRequest) = Task.singleton <| storage[c.ID] <- c

  let delete (id: Guid) =
    storage.TryRemove(id) |> ignore |> Task.singleton

let optionToObjResult<'a> (topt: Task<'a option>) =
  topt
  |> Task.map (function
    | Some v -> v |> box |> Ok
    | None -> NotFound typeof<'a>.Name |> Error)

let query<'a> : Configuration -> Guid -> Task<Result<'a, DomainError>> =
  fun _ id ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> -> Sessions.query id |> optionToObjResult<Session>
        | t when t = typeof<User> -> Users.query id |> optionToObjResult<User>
        | t when t = typeof<Channel> -> Channels.query id |> optionToObjResult<Channel>
        | t when t = typeof<EditingCommunicationsRequest> ->
          EditingCommunicationsRequests.query id
          |> optionToObjResult<EditingCommunicationsRequest>
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return value :?> 'a
    }

let getAll<'a> : Configuration -> unit -> Task<Result<'a list, DomainError>> =
  fun _ () ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> -> Sessions.getAll () |> TaskResult.map box
        | t when t = typeof<User> -> Users.getAll () |> TaskResult.map box
        | t when t = typeof<Channel> -> Channels.getAll () |> TaskResult.map box
        | t when t = typeof<EditingCommunicationsRequest> ->
          EditingCommunicationsRequests.getAll ()
          |> TaskResult.map box
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return value :?> 'a list
    }

let queryPredicate<'a> : Configuration -> ('a -> bool) -> Task<Result<'a, DomainError>> =
  fun _ predicate ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> ->
          box predicate :?> Session -> bool
          |> Sessions.find
          |> optionToObjResult<Session>
        | t when t = typeof<User> ->
          box predicate :?> User -> bool
          |> Users.find
          |> optionToObjResult<User>
        | t when t = typeof<Channel> ->
          box predicate :?> Channel -> bool
          |> Channels.find
          |> optionToObjResult<Channel>
        | t when t = typeof<EditingCommunicationsRequest> ->
          box predicate :?> EditingCommunicationsRequest -> bool
          |> EditingCommunicationsRequests.find
          |> optionToObjResult<EditingCommunicationsRequest>
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return value :?> 'a
    }

let save<'a> : Configuration -> 'a -> Task<Result<unit, DomainError>> =
  fun _ a ->
    taskResult {
      do!
        match typeof<'a> with
        | t when t = typeof<Session> -> box a :?> Session |> Sessions.save |> Task.map Ok
        | t when t = typeof<User> -> box a :?> User |> Users.save |> Task.map Ok
        | t when t = typeof<Channel> -> box a :?> Channel |> Channels.save |> Task.map Ok
        | t when t = typeof<EditingCommunicationsRequest> ->
          box a :?> EditingCommunicationsRequest
          |> EditingCommunicationsRequests.save
          |> Task.map Ok
        | t ->
          InternalServerError $"Save not implemented for type {t.FullName}"
          |> TaskResult.error
    }

let delete<'a> : Configuration -> Guid -> Task<Result<unit, DomainError>> =
  fun _ id ->
    taskResult {
      do!
        match typeof<'a> with
        | t when t = typeof<Session> -> Sessions.delete id |> Task.map Ok
        | t when t = typeof<User> -> Users.delete id |> Task.map Ok
        | t when t = typeof<Channel> -> Channels.delete id |> Task.map Ok
        | t when t = typeof<EditingCommunicationsRequest> ->
          EditingCommunicationsRequests.delete id
          |> Task.map Ok
        | t ->
          InternalServerError $"Delete not implemented for type {t.FullName}"
          |> TaskResult.error
    }
