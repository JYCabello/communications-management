module CommunicationsManagement.API.Effects

open System
open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling

type IPorts =
  abstract member sendEvent: SendEventParams -> Task<Result<unit, DomainError>>
  abstract member sendNotification: SendNotificationParams -> Task<Result<unit, DomainError>>
  abstract member configuration: Configuration
  abstract member query<'a> : Guid -> Task<Result<'a, DomainError>>
  abstract member save<'a> : 'a -> Task<Result<unit, DomainError>>

type Effect<'a> = IPorts -> Task<Result<'a, DomainError>>

let mapE (f: 'a -> 'b) (e: Effect<'a>) : Effect<'b> = fun p -> p |> e |> TaskResult.map f

let bindE (f: 'a -> Effect<'b>) (e: Effect<'a>) : Effect<'b> =
  fun p ->
    taskResult {
      let! a = e p
      return! p |> f a
    }

let fromTR ar : Effect<'a> = fun _ -> ar
let fromResult r : Effect<'a> = fun _ -> r |> Task.FromResult
let singleton a : Effect<'a> = fun _ -> a |> TaskResult.ok
let error e : Effect<'a> = fun _ -> e |> TaskResult.error
let fromTask t : Effect<'a> = fun _ -> t |> Task.map Ok

let fromOption rn o : Effect<'a> =
  match o with
  | Some a -> a |> singleton
  | None -> NotFound rn |> error

let fromTaskOption rn tskOpt : Effect<'a> =
  fun _ ->
    task {
      let! o = tskOpt

      return
        match o with
        | Some a -> Ok a
        | None -> NotFound rn |> Error
    }

type EffectBuilder() =
  member inline this.Bind(e: Effect<'a>, f: 'a -> Effect<'b>) : Effect<'b> = bindE f e
  member inline this.Return a : Effect<'a> = fun _ -> TaskResult.ok a
  member inline this.ReturnFrom (e: Effect<'a>) : Effect<'a> = e
  member inline this.Zero() : Effect<Unit> = fun _ -> TaskResult.ok ()
  member inline this.Combine(a, b) = a |> bindE (fun _ -> b)

  member inline this.MergeSources(ea: Effect<'a>, eb: Effect<'b>) : Effect<'a * 'b> =
    this.Bind(ea, (fun a -> eb |> mapE (fun b -> (a, b))))

  member inline _.Source(tsk: Task<'a>) : Effect<'a> = tsk |> fromTask
  member inline _.Source(r: Result<'a, DomainError>) : Effect<'a> = r |> fromResult
  member inline _.Source(tr: Task<Result<'a, DomainError>>) : Effect<'a> = tr |> fromTR
  member inline _.Source(bt: IPorts -> Task<Result<'a, DomainError>>): Effect<'a> = bt

let effect = EffectBuilder()

let attempt (tr: Task<Result<'a, DomainError>>) : Task<Result<'a, DomainError>> =
  task {
    try
      return! tr
    with
    | _ ->
      return
        "Something happened"
        |> InternalServerError
        |> Error
  }
