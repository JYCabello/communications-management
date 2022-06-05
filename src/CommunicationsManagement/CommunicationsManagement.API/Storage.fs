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

let private querySession: Guid -> Task<Option<Session>> =
  fun id -> sessionStorage |> tryGet id |> Task.FromResult

let toResult (topt: Task<'a option>) =
  task {
    let! opt = topt

    return
      match opt with
      | Some v -> Ok v
      | None -> NotFound typeof<'a>.Name |> Error
  }

let query<'a> : Configuration -> Guid -> Task<Result<'a, DomainError>> =
  fun _ id ->
    taskResult {
      let! value =
        match typeof<'a> with
        | t when t = typeof<Session> -> querySession id |> toResult
        | t ->
          InternalServerError $"Query not implemented for type {t.FullName}"
          |> TaskResult.error

      return box value :?> 'a
    }
