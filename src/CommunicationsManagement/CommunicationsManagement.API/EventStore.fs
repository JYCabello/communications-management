module CommunicationsManagement.API.EventStore

open System
open System.Collections.Generic
open System.Text
open System.Threading
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open EventStore.Client
open Newtonsoft.Json
open FsToolkit.ErrorHandling


type SubscriptionDetails =
  { StreamID: string
    Handler: StreamSubscription -> ResolvedEvent -> CancellationToken -> Task }

let getClient cs =
  let settings = EventStoreClientSettings.Create cs
  new EventStoreClient(settings)

let state = Dictionary<int, int>()

let mutable checkpoint: StreamPosition option = None

let getCheckpoint () =
  checkpoint
  |> Option.map FromStream.After
  |> Option.defaultValue FromStream.Start

let deserialize (evnt: ResolvedEvent) =
  let decoded =
    evnt.Event.Data.ToArray()
    |> Encoding.UTF8.GetString

  match evnt.Event.EventType with
  | "Message" ->
    try
      decoded
      |> JsonConvert.DeserializeObject<Message>
      |> Message
    with
    | _ -> StreamEvent.Toxic { Type = "Message"; Content = decoded }
  | t -> StreamEvent.Toxic { Type = t; Content = decoded }

let private handleMessage (m: Message) =
  task {
    if not <| state.ContainsKey(m.ID) then
      state[m.ID] <- m.Amount
    else
      state[m.ID] <- state[m.ID] + m.Amount
  }

let handle =
  function
  | Message m -> m |> handleMessage
  | Toxic _ -> Task.singleton ()

let handleEvent (evnt: ResolvedEvent) =
  task {
    do! deserialize evnt |> handle
    return checkpoint <- Some evnt.OriginalEventNumber
  }

let subscribe cs (subscription: SubscriptionDetails) =
  let rec subscribeTo () =
    let reSubscribe (_: StreamSubscription) (reason: SubscriptionDroppedReason) (_: Exception) =
      if reason = SubscriptionDroppedReason.Disposed |> not then
        subscribeTo () |> ignore
      else
        ()

    task {
      try
        do!
          (getClient cs)
            .SubscribeToStreamAsync(
              subscription.StreamID,
              getCheckpoint (),
              subscription.Handler,
              false,
              reSubscribe
            )
          :> Task
      with
      | _ ->
        do! Task.Delay(5000)
        return! subscribeTo ()
    }

  subscribeTo () |> ignore

let triggerSubscriptions (ports: IPorts) =
  let sub = subscribe ports.configuration.EventStoreConnectionString

  { StreamID = "deletable"
    Handler = fun _ evnt _ -> task { do! handleEvent evnt } :> Task }
  |> sub
