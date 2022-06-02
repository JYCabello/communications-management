module CommunicationsManagement.API.Effects

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open FsToolkit.ErrorHandling

type IPorts =
  abstract member sendEvent : SendEventParams -> Task<Result<unit, DomainError>>
  abstract member sendNotification : SendNotificationParams -> Task<Result<unit, DomainError>>

type Effect<'a> = IPorts -> Task<Result<'a, DomainError>>

let mapE (f: 'a -> 'b) (e: Effect<'a>) : Effect<'b> = fun p -> p |> e |> TaskResult.map f

let bindE (f: 'a -> Effect<'b>) (e: Effect<'a>) : Effect<'b> =
  fun p ->
    taskResult {
      let! a = e p
      return! p |> f a
    }


let fromResult r : Effect<'a> = fun _ -> r
let singleton a : Effect<'a> = fun _ -> a |> TaskResult.ok
let error e : Effect<'a> = fun _ -> e |> TaskResult.error

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

let getPorts (p: IPorts) = p |> TaskResult.ok

type EffectBuilder() =
  member this.Bind(x: Effect<'a>, f: 'a -> Effect<'b>) : Effect<'b> = bindE f x
  member this.Return x : Effect<'a> = fun _ -> TaskResult.ok x
  member this.ReturnFrom x = fun (_: IPorts) -> x
  member this.Zero() : Effect<Unit> = fun _ -> TaskResult.ok ()
  member this.Combine(a, b) = a |> bindE (fun _ -> b)

let effect = EffectBuilder()
