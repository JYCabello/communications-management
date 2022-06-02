module CommunicationsManagement.API.Effects

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling

type IPorts =
  abstract member sendEvent<'a> : SendEventParams<'a> -> Task
  abstract member sendNotification : SendNotificationParams -> Task

type Effect<'a> = IPorts -> Task<Result<'a, DomainError>>

let mapE (f: 'a -> 'b) (e: Effect<'a>) : Effect<'b> = fun p -> p |> e |> TaskResult.map f

let bindE (f: 'a -> Effect<'b>) (e: Effect<'a>) : Effect<'b> =
  fun p ->
    taskResult {
      let! a = e p
      return! p |> f a
    }

let getPorts (p: IPorts) = p |> TaskResult.ok

type EffectBuilder() =
  member this.Bind(x: Effect<'a>, f: 'a -> Effect<'b>) : Effect<'b> = bindE f x
  member this.Return x : Effect<'a> = fun _ -> TaskResult.ok x
  member this.ReturnFrom x = fun (_: IPorts) -> x
  member this.Zero() : Effect<Unit> = fun _ -> TaskResult.ok ()
  member this.Combine(a, b) = a |> bindE (fun _ -> b)

let effect = EffectBuilder()
