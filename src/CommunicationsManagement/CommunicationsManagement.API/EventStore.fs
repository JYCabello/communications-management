module CommunicationsManagement.API.EventStore

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Models
open EventStore.Client
open Configuration
open Newtonsoft.Json

let settings =
  EventStoreClientSettings.Create configuration.EventStoreConnectionString

let client = new EventStoreClient(settings)

let state = Dictionary<int, int>()

let mutable checkpoint: StreamPosition option = None

let getCheckpoint () =
  checkpoint
  |> Option.map FromStream.After
  |> Option.defaultValue FromStream.Start

let handleEvent (evnt: ResolvedEvent) =
  task {
    let data = evnt.Event.Data.ToArray()
    let decoded = Encoding.UTF8.GetString(data)
    let parsed = JsonConvert.DeserializeObject<Message>(decoded)

    if not <| state.ContainsKey(parsed.ID) then
      state[parsed.ID] <- parsed.Amount
    else
      state[parsed.ID] <- state[parsed.ID] + parsed.Amount

    checkpoint <- Some evnt.OriginalEventNumber

    return ()
  }

let rec subscribe () =
  let reSubscribe (_: StreamSubscription) (reason: SubscriptionDroppedReason) (_: Exception) =
    if reason = SubscriptionDroppedReason.Disposed |> not then
      subscribe () |> ignore
    else
      ()

  let handle _ evnt _ = task { do! handleEvent evnt } :> Task

  task {
    try
      return!
        client
          .SubscribeToStreamAsync("deletable", getCheckpoint(), handle, false, reSubscribe)
          :> Task
    with
    | _ ->
      do! Task.Delay(5000)
      return! subscribe ()
  }

subscribe () |> ignore
