module CommunicationsManagement.API.EventStore

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open CommunicationsManagement.API.Models
open EventStore.Client
open Configuration
open Newtonsoft.Json
open FsToolkit.ErrorHandling

let settings =
  EventStoreClientSettings.Create configuration.EventStoreConnectionString

let client = new EventStoreClient(settings)

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
    | _ -> StreamEvent.Toxic("Message", decoded)
  | t -> StreamEvent.Toxic(t, decoded)

let private handleMessage m =
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

let subscribe (subscription: SubscriptionDetails) =
  let rec subscribeTo () =
    let reSubscribe (_: StreamSubscription) (reason: SubscriptionDroppedReason) (_: Exception) =
      if reason = SubscriptionDroppedReason.Disposed |> not then
        subscribeTo () |> ignore
      else
        ()

    task {
      try
        return!
          client
            .SubscribeToStreamAsync(
              subscription.StreamID,
              getCheckpoint (),
              subscription.Handler,
              false,
              reSubscribe)
          :> Task
      with
      | _ ->
        do! Task.Delay(5000)
        return! subscribeTo ()
    }

  subscribeTo () |> ignore

{ StreamID = "deletable"
  Handler = fun _ evnt _ -> task { do! handleEvent evnt } :> Task }
|> subscribe
