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

let private sessionStorage = ConcurrentDictionary<Guid, Session>()
let private userStorage = ConcurrentDictionary<Guid, User>()

let private querySession id =
  sessionStorage |> tryGet id |> Task.FromResult

let private findSession q =
  sessionStorage.ToArray()
  |> Seq.tryFind (fun kvp -> q kvp.Value)
  |> Option.map (fun kvp -> kvp.Value)
  |> Task.FromResult

let private queryUser id =
  userStorage |> tryGet id |> Task.FromResult

let private findUser q =
  userStorage.ToArray()
  |> Seq.tryFind (fun kvp -> q kvp.Value)
  |> Option.map (fun kvp -> kvp.Value)
  |> Task.FromResult

let private saveSession (s: Session) =
  Task.FromResult <| sessionStorage[s.ID] <- s

let private saveUser (u: User) =
  Task.FromResult <| userStorage[u.ID] <- u

let toObjResult (topt: Task<'a option>) =
  task {
    let! opt = topt

    return
      match opt with
      | Some v -> Ok v
      | None -> NotFound typeof<'a>.Name |> Error
      |> Result.map box
  }

let query<'a> : Configuration -> Guid -> Task<Result<'a, DomainError>> =
  fun _ id ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> -> querySession id |> toObjResult
        | t when t = typeof<User> -> queryUser id |> toObjResult
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return value :?> 'a
    }

let queryPredicate<'a> : Configuration -> ('a -> bool) -> Task<Result<'a, DomainError>> =
  fun _ predicate ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> ->
          box predicate :?> Session -> bool
          |> findSession
          |> toObjResult
        | t when t = typeof<User> ->
          box predicate :?> User -> bool
          |> findUser
          |> toObjResult
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
        | t when t = typeof<Session> -> box a :?> Session |> saveSession |> Task.map Ok
        | t when t = typeof<User> -> box a :?> User |> saveUser |> Task.map Ok
        | t ->
          InternalServerError $"Save not implemented for type {t.FullName}"
          |> TaskResult.error
    }
