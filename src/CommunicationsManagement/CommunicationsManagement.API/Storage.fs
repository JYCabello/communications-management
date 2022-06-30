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
  let private sessionStorage = ConcurrentDictionary<Guid, Session>()

  let query id =
    sessionStorage |> tryGet id |> Task.FromResult

  let getAll () =
    sessionStorage.Values
    |> Seq.toList
    |> TaskResult.ok

  let save (s: Session) =
    Task.FromResult <| sessionStorage[s.ID] <- s

  let find q =
    sessionStorage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.FromResult

  let delete (id: Guid) =
    sessionStorage.TryRemove(id) |> ignore
    Task.singleton ()

[<RequireQualifiedAccess>]
module Users =
  let private userStorage = ConcurrentDictionary<Guid, User>()

  let query id =
    userStorage |> tryGet id |> Task.singleton

  let getAll () =
    userStorage.Values
    |> Seq.sortByDescending (fun u ->
      match u.LastLogin with
      | None -> DateTime.MinValue
      | Some ll -> ll)
    |> Seq.toList
    |> TaskResult.ok

  let find q =
    userStorage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save (u: User) =
    Task.singleton <| userStorage[u.ID] <- u

  let delete (id: Guid) =
    userStorage.TryRemove(id)
    |> ignore
    |> Task.singleton

[<RequireQualifiedAccess>]
module Channels =
  let private channelStorage = ConcurrentDictionary<Guid, Channel>()

  let query id =
    channelStorage |> tryGet id |> Task.singleton

  let getAll () =
    channelStorage.Values
    |> Seq.sortByDescending (fun c -> c.IsEnabled)
    |> Seq.toList
    |> TaskResult.ok

  let find q =
    channelStorage.ToArray()
    |> Seq.tryFind (fun kvp -> q kvp.Value)
    |> Option.map (fun kvp -> kvp.Value)
    |> Task.singleton

  let save (c: Channel) =
    Task.singleton <| channelStorage[c.ID] <- c

  let delete (id: Guid) =
    channelStorage.TryRemove(id)
    |> ignore
    |> Task.singleton

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
        | t ->
          InternalServerError $"Delete not implemented for type {t.FullName}"
          |> TaskResult.error
    }
