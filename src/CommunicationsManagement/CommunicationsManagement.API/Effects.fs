module CommunicationsManagement.API.Effects

open System
open System.Threading.Tasks
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Models.NotificationModels
open FsToolkit.ErrorHandling

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

let attempt (tr: Task<Result<'a, DomainError>>) : Task<Result<'a, DomainError>> =
  task {
    try
      return! tr
    with
    | e ->
      return
        e.Message
        |> sprintf "Something happened: %s"
        |> InternalServerError
        |> Error
  }

type FreeRailway<'dep, 'ok, 'err> = 'dep -> Task<Result<'ok, 'err>>

let mapFR (f: 'a -> 'b) (fr: FreeRailway<'dep, 'a, 'err>) : FreeRailway<'dep, 'b, 'err> =
  fun d -> d |> fr |> TaskResult.map f

let bindFR
  (f: 'a -> FreeRailway<'dep, 'b, 'err>)
  (fr: FreeRailway<'dep, 'a, 'err>)
  : FreeRailway<'dep, 'b, 'err> =
  fun d ->
    taskResult {
      let! a = fr d
      return! d |> f a
    }

type FreeRailwayBuilder() =
  let fromTask (ta: Task<'a>) : FreeRailway<'dep, 'a, 'err> = fun _ -> ta |> Task.map Ok
  let fromTaskVoid (t: Task) : FreeRailway<'dep, unit, 'err> = task { do! t } |> fromTask
  let fromResult (r: Result<'a, 'e>) : FreeRailway<'dep, 'a, 'e> = fun _ -> r |> Task.singleton
  let fromTR (tr: Task<Result<'a, 'e>>) : FreeRailway<'dep, 'a, 'e> = fun _ -> tr

  member inline this.Bind
    (
      e: FreeRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] f: 'a -> FreeRailway<'dep, 'b, 'err>
    ) : FreeRailway<'dep, 'b, 'err> =
    bindFR f e

  member inline this.Return a : FreeRailway<'dep, 'a, 'err> = fun _ -> TaskResult.ok a
  member inline this.ReturnFrom(e: FreeRailway<'dep, 'a, 'err>) : FreeRailway<'dep, 'a, 'err> = e
  member inline this.Zero() : FreeRailway<'dep, unit, 'err> = fun _ -> TaskResult.ok ()

  member inline this.Combine
    (
      a: FreeRailway<'dep, 'a, 'err>,
      b: FreeRailway<'dep, 'b, 'err>
    ) : FreeRailway<'dep, 'b, 'err> =
    a |> bindFR (fun _ -> b)

  member inline _.TryWith
    (
      e: FreeRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] handler: Exception -> FreeRailway<'dep, 'a, 'err>
    ) : FreeRailway<'dep, 'a, 'err> =
    fun p ->
      task {
        try
          return! e p
        with
        | e -> return! handler e p
      }

  member inline _.TryFinally
    (
      e: FreeRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] compensation: unit -> unit
    ) : FreeRailway<'dep, 'a, 'err> =
    fun p ->
      task {
        try
          return! e p
        finally
          do compensation ()
      }

  member inline _.Using
    (
      r: 'r :> IDisposable,
      [<InlineIfLambda>] binder: 'r -> FreeRailway<'dep, 'a, 'err>
    ) : FreeRailway<'dep, 'a, 'err> =
    fun p ->
      task {
        use rd = r
        return! binder rd p
      }

  member inline this.While
    (
      [<InlineIfLambda>] guard: unit -> bool,
      computation: FreeRailway<'dep, unit, 'err>
    ) : FreeRailway<'dep, unit, 'err> =
    if guard () then
      let mutable whileAsync = Unchecked.defaultof<_>

      whileAsync <-
        this.Bind(
          computation,
          (fun () ->
            if guard () then
              whileAsync
            else
              this.Zero())
        )

      whileAsync
    else
      this.Zero()

  member inline _.BindReturn
    (
      x: FreeRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] f: 'a -> 'b
    ) : FreeRailway<'dep, 'b, 'err> =
    mapFR f x

  member inline this.MergeSources
    (
      ea: FreeRailway<'dep, 'a, 'err>,
      eb: FreeRailway<'dep, 'b, 'err>
    ) : FreeRailway<'dep, 'a * 'b, 'err> =
    this.Bind(ea, (fun a -> eb |> mapFR (fun b -> (a, b))))

  member inline _.Source<'a, 'dep, 'err when 'a: not struct>
    (tsk: Task<'a>)
    : FreeRailway<'dep, 'a, 'err> =
    tsk |> fromTask

  member inline _.Source(tsk: Task) : FreeRailway<'dep, unit, 'err> = tsk |> fromTaskVoid
  member inline _.Source(r: Result<'a, 'err>) : FreeRailway<'dep, 'a, 'err> = r |> fromResult
  member inline _.Source(tr: Task<Result<'a, 'err>>) : FreeRailway<'dep, 'a, 'err> = tr |> fromTR

let freeRW = FreeRailwayBuilder()

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

let fromTaskVoid (t: Task) : Effect<unit> =
  task {
    do! t
    return ()
  }
  |> fromTask

let fromOption rn o : Effect<'a> =
  match o with
  | Some a -> a |> singleton
  | None -> NotFound rn |> error

let fromTaskOption rn tskOpt : Effect<'a> =
  tskOpt
  |> Task.map (function
    | Some a -> Ok a
    | None -> NotFound rn |> Error)
  |> fromTR

type EffectBuilder() =
  member inline this.Bind(e: Effect<'a>, [<InlineIfLambda>] f: 'a -> Effect<'b>) : Effect<'b> =
    bindE f e

  member inline this.Return a : Effect<'a> = fun _ -> TaskResult.ok a
  member inline this.ReturnFrom(e: Effect<'a>) : Effect<'a> = e
  member inline this.Zero() : Effect<Unit> = fun _ -> TaskResult.ok ()
  member inline this.Combine(a: Effect<'a>, b: Effect<'b>) : Effect<'b> = a |> bindE (fun _ -> b)

  member inline _.TryWith
    (
      e: Effect<'a>,
      [<InlineIfLambda>] handler: Exception -> Effect<'a>
    ) : Effect<'a> =
    fun p ->
      task {
        try
          return! e p
        with
        | e -> return! handler e p
      }

  member inline _.TryFinally
    (
      e: Effect<'a>,
      [<InlineIfLambda>] compensation: unit -> unit
    ) : Effect<'a> =
    fun p ->
      task {
        try
          return! e p
        finally
          do compensation ()
      }

  member inline _.Using
    (
      r: 'r :> IDisposable,
      [<InlineIfLambda>] binder: 'r -> Effect<'a>
    ) : Effect<'a> =
    fun p ->
      task {
        use rd = r
        return! binder rd p
      }

  member inline this.While
    (
      [<InlineIfLambda>] guard: unit -> bool,
      computation: Effect<unit>
    ) : Effect<unit> =
    if guard () then
      let mutable whileAsync = Unchecked.defaultof<_>

      whileAsync <-
        this.Bind(
          computation,
          (fun () ->
            if guard () then
              whileAsync
            else
              this.Zero())
        )

      whileAsync
    else
      this.Zero()

  member inline _.BindReturn(x: Effect<'a>, [<InlineIfLambda>] f: 'a -> 'b) : Effect<'b> = mapE f x

  member inline this.MergeSources(ea: Effect<'a>, eb: Effect<'b>) : Effect<'a * 'b> =
    this.Bind(ea, (fun a -> eb |> mapE (fun b -> (a, b))))

  member inline _.Source<'a when 'a: not struct>(tsk: Task<'a>) : Effect<'a> = tsk |> fromTask
  member inline _.Source(tsk: Task) : Effect<unit> = tsk |> fromTaskVoid
  member inline _.Source(r: Result<'a, DomainError>) : Effect<'a> = r |> fromResult
  member inline _.Source(tr: Task<Result<'a, DomainError>>) : Effect<'a> = tr |> fromTR
  member inline _.Source(bt: IPorts -> Task<Result<'a, DomainError>>) : Effect<'a> = bt

let effect = EffectBuilder()

let getPorts: Effect<IPorts> = fun p -> TaskResult.ok p
let emit e : Effect<unit> = fun p -> p.sendEvent e
let getAll<'a> : Effect<'a list> = fun p -> p.getAll<'a> ()
let find<'a> id : Effect<'a> = fun p -> p.find id
let query<'a> q : Effect<'a> = fun p -> p.query q
let save<'a> a : Effect<unit> = fun p -> p.save<'a> a
let delete<'a> a : Effect<unit> = fun p -> p.delete<'a> a
let solve p (e: Effect<'a>) = e p
