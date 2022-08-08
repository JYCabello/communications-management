module CommunicationsManagement.API.Effects

open System
open System.Threading.Tasks
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Ports
open FsToolkit.ErrorHandling
open Giraffe
open Microsoft.AspNetCore.Http


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

type ReaderRailway<'dep, 'ok, 'err> = 'dep -> Task<Result<'ok, 'err>>

let mapFR (f: 'a -> 'b) (fr: ReaderRailway<'dep, 'a, 'err>) : ReaderRailway<'dep, 'b, 'err> =
  fun d -> d |> fr |> TaskResult.map f

let bindFR
  (f: 'a -> ReaderRailway<'dep, 'b, 'err>)
  (fr: ReaderRailway<'dep, 'a, 'err>)
  : ReaderRailway<'dep, 'b, 'err> =
  fun d ->
    taskResult {
      let! a = fr d
      return! d |> f a
    }

module FRBConverters =
  let fromTask (ta: Task<'a>) : ReaderRailway<'dep, 'a, 'err> = fun _ -> ta |> Task.map Ok
  let fromTaskVoid (t: Task) : ReaderRailway<'dep, unit, 'err> = task { do! t } |> fromTask
  let fromResult (r: Result<'a, 'e>) : ReaderRailway<'dep, 'a, 'e> = fun _ -> r |> Task.singleton
  let fromTR (tr: Task<Result<'a, 'e>>) : ReaderRailway<'dep, 'a, 'e> = fun _ -> tr

type Rail<'a> = ReaderRailway<IPorts, 'a, DomainError>
type RailRoute<'a> = ReaderRailway<IPorts * HttpFunc * HttpContext, 'a, DomainError>

type FreeRailwayBuilder() =
  member inline this.Bind
    (
      rr: ReaderRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] f: 'a -> ReaderRailway<'dep, 'b, 'err>
    ) : ReaderRailway<'dep, 'b, 'err> =
    bindFR f rr

  member inline this.Return a : ReaderRailway<'dep, 'a, 'err> = fun _ -> TaskResult.ok a

  member inline this.ReturnFrom(rr: ReaderRailway<'dep, 'a, 'err>) : ReaderRailway<'dep, 'a, 'err> =
    rr

  member inline this.Zero() : ReaderRailway<'dep, unit, 'err> = fun _ -> TaskResult.ok ()

  member inline this.Combine
    (
      a: ReaderRailway<'dep, 'a, 'err>,
      b: ReaderRailway<'dep, 'b, 'err>
    ) : ReaderRailway<'dep, 'b, 'err> =
    a |> bindFR (fun _ -> b)

  member inline _.TryWith
    (
      rr: ReaderRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] handler: Exception -> ReaderRailway<'dep, 'a, 'err>
    ) : ReaderRailway<'dep, 'a, 'err> =
    fun p ->
      task {
        try
          return! rr p
        with
        | e -> return! handler e p
      }

  member inline _.TryFinally
    (
      rr: ReaderRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] compensation: unit -> unit
    ) : ReaderRailway<'dep, 'a, 'err> =
    fun p ->
      task {
        try
          return! rr p
        finally
          do compensation ()
      }

  member inline _.Using
    (
      r: 'r :> IDisposable,
      [<InlineIfLambda>] binder: 'r -> ReaderRailway<'dep, 'a, 'err>
    ) : ReaderRailway<'dep, 'a, 'err> =
    fun p ->
      task {
        use rd = r
        return! binder rd p
      }

  member inline this.While
    (
      [<InlineIfLambda>] guard: unit -> bool,
      computation: ReaderRailway<'dep, unit, 'err>
    ) : ReaderRailway<'dep, unit, 'err> =
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
      x: ReaderRailway<'dep, 'a, 'err>,
      [<InlineIfLambda>] f: 'a -> 'b
    ) : ReaderRailway<'dep, 'b, 'err> =
    mapFR f x

  member inline this.MergeSources
    (
      ea: ReaderRailway<'dep, 'a, 'err>,
      eb: ReaderRailway<'dep, 'b, 'err>
    ) : ReaderRailway<'dep, 'a * 'b, 'err> =
    this.Bind(ea, (fun a -> eb |> mapFR (fun b -> (a, b))))

  member inline _.Source<'a, 'dep, 'err when 'a: not struct>
    (tsk: Task<'a>)
    : ReaderRailway<'dep, 'a, 'err> =
    tsk |> FRBConverters.fromTask

  member inline _.Source(tsk: Task) : ReaderRailway<'dep, unit, 'err> =
    tsk |> FRBConverters.fromTaskVoid

  member inline _.Source(r: Result<'a, 'err>) : ReaderRailway<'dep, 'a, 'err> =
    r |> FRBConverters.fromResult

  member inline _.Source(tr: Task<Result<'a, 'err>>) : ReaderRailway<'dep, 'a, 'err> =
    tr |> FRBConverters.fromTR

  member inline _.Source<'dep, 'a, 'err when 'a: not struct>
    (tsk: Task<'a>)
    : ReaderRailway<'dep, 'a, 'err> =
    tsk |> FRBConverters.fromTask

  member inline _.Source(bt: 'dep -> Task<Result<'a, 'err>>) : ReaderRailway<'dep, 'a, 'err> = bt

  member inline _.Source
    (pvr: IPorts -> TaskEffectValidateResult<'a>)
    : RailRoute<ValidateResult<'a>> =
    fun (p, _, _) ->
      task {
        let! vr = pvr p

        return
          match vr with
          | EffectValid a -> a |> Valid |> Ok
          | EffectInvalid ve -> ve |> Invalid |> Ok
          | EffectFail de -> de |> Error
      }

let rail = FreeRailwayBuilder()

module EffectOps =
  let getPorts: Rail<IPorts> = fun p -> TaskResult.ok p
  let emit e : Rail<unit> = fun p -> p.sendEvent e
  let getAll<'a> : Rail<'a list> = fun p -> p.getAll<'a> ()
  let find<'a> id : Rail<'a> = fun p -> p.find id
  let query<'a> q : Rail<'a> = fun p -> p.query q
  let save<'a> a : Rail<unit> = fun p -> p.save<'a> a
  let delete<'a> a : Rail<unit> = fun p -> p.delete<'a> a
  let solve p (e: Rail<'a>) = e p

module EffectRouteOps =
  let getPorts: RailRoute<IPorts> = fun (p, _, _) -> TaskResult.ok p
  let emit e : RailRoute<unit> = fun (p, _, _) -> p.sendEvent e
  let getAll<'a> : RailRoute<'a list> = fun (p, _, _) -> p.getAll<'a> ()
  let find<'a> id : RailRoute<'a> = fun (p, _, _) -> p.find id
  let query<'a> q : RailRoute<'a> = fun (p, _, _) -> p.query q
  let save<'a> a : RailRoute<unit> = fun (p, _, _) -> p.save<'a> a
  let delete<'a> a : RailRoute<unit> = fun (p, _, _) -> p.delete<'a> a
