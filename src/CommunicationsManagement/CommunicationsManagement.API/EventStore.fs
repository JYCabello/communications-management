[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.EventStore

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Ports
open EventStore.Client
open Newtonsoft.Json
open FsToolkit.ErrorHandling
open EffectOps

type SubscriptionDetails =
  { Handler: StreamSubscription -> ResolvedEvent -> CancellationToken -> Task
    GetCheckpoint: unit -> Task<FromAll>
    SaveCheckpoint: Position option -> Task<unit> }

let getClient cs =
  let settings = EventStoreClientSettings.Create cs
  new EventStoreClient(settings)

let deserialize (evnt: ResolvedEvent) =
  let decoded =
    evnt.Event.Data.ToArray()
    |> Encoding.UTF8.GetString

  try
    match evnt.Event.EventType with
    | "SessionCreated" ->
      decoded
      |> JsonConvert.DeserializeObject<SessionCreated>
      |> SessionCreated
    | "SessionTerminated" ->
      decoded
      |> JsonConvert.DeserializeObject<SessionTerminated>
      |> SessionTerminated
    | "UserCreated" ->
      decoded
      |> JsonConvert.DeserializeObject<UserCreated>
      |> UserCreated
    | "RoleAdded" ->
      decoded
      |> JsonConvert.DeserializeObject<RoleAdded>
      |> RoleAdded
    | "RoleRemoved" ->
      decoded
      |> JsonConvert.DeserializeObject<RoleRemoved>
      |> RoleRemoved
    | "ChannelCreated" ->
      decoded
      |> JsonConvert.DeserializeObject<ChannelCreated>
      |> ChannelCreated
    | "ChannelEnabled" ->
      decoded
      |> JsonConvert.DeserializeObject<ChannelEnabled>
      |> ChannelEnabled
    | "ChannelDisabled" ->
      decoded
      |> JsonConvert.DeserializeObject<ChannelDisabled>
      |> ChannelDisabled
    | t -> StreamEvent.Toxic { Type = t; Content = decoded }
  with
  | _ -> StreamEvent.Toxic { Type = "Message"; Content = decoded }

// Ignore errors just because it's a pet project, logs go in here.
// I also want to learn more about what to do in this case.
let private ignoreErrors =
  Task.map (fun r ->
    match r with
    | Ok u -> u
    | Error _ -> ())

let private handle (se: ResolvedEvent) (ports: IPorts) : Task<unit> =
  let userCreated (uc: UserCreated) =
    save<User>
      { Name = uc.Name
        Email = uc.Email |> Email
        ID = uc.UserID
        Roles = uc.Roles
        LastLogin = None }

  let roleAdded (ra: RoleAdded) =
    effect {
      let! user = find<User> ra.UserID
      do! save<User> { user with Roles = user.Roles + ra.RoleToAdd }
    }

  let roleRemoved (rr: RoleRemoved) =
    effect {
      let! user = find<User> rr.UserID
      do! save<User> { user with Roles = user.Roles - rr.RoleRemoved }
    }

  let sessionCreated (sc: SessionCreated) =
    effect {
      let! user = find<User> sc.UserID
      do! save<User> { user with LastLogin = se.OriginalEvent.Created |> Some }

      do!
        save<Session>
          { ID = sc.SessionID
            UserID = sc.UserID
            ExpiresAt = sc.ExpiresAt }
    }

  let sessionTerminated (st: SessionTerminated) = delete<Session> st.SessionID

  let channelCreated (e: ChannelCreated) =
    save<Channel>
      { ID = e.ChannelID
        Name = e.ChannelName
        IsEnabled = true }

  let channelEnabled (e: ChannelEnabled) =
    effect {
      let! channel = find<Channel> e.ChannelID
      do! save { channel with IsEnabled = true }
    }

  let channelDisabled (e: ChannelDisabled) =
    effect {
      let! channel = find<Channel> e.ChannelID
      do! save { channel with IsEnabled = false }
    }

  match deserialize se with
  | SessionCreated sc -> sessionCreated sc
  | SessionTerminated st -> sessionTerminated st
  | UserCreated uc -> userCreated uc
  | RoleAdded ra -> roleAdded ra
  | RoleRemoved rr -> roleRemoved rr
  | ChannelCreated cc -> channelCreated cc
  | ChannelEnabled ce -> channelEnabled ce
  | ChannelDisabled cd -> channelDisabled cd
  | _ -> effect { return () }
  |> solve ports
  |> ignoreErrors

let subscribe cs (subscription: SubscriptionDetails) =
  let rec subscribeTo () =
    let reSubscribe (_: StreamSubscription) (reason: SubscriptionDroppedReason) (_: Exception) =
      if reason = SubscriptionDroppedReason.Disposed |> not then
        subscribeTo () |> ignore
      else
        ()

    task {
      try
        let! checkpoint = subscription.GetCheckpoint()

        let handler: StreamSubscription -> ResolvedEvent -> CancellationToken -> Task =
          fun s e c ->
            task {
              do! subscription.Handler s e c
              do! subscription.SaveCheckpoint(e.OriginalPosition |> Option.ofNullable)
            }

        let client = getClient cs

        do! client.SubscribeToAllAsync(checkpoint, handler, false, reSubscribe) :> Task
      with
      | _ ->
        do! Task.Delay(5000)
        return! subscribeTo ()
    }

  subscribeTo ()

let sendEvent (c: Configuration) (e: SendEventParams) : Task<Result<unit, DomainError>> =
  taskResult {
    use cl = getClient c.EventStoreConnectionString
    let sn = getStreamName e.Event
    let etn = getEventTypeName e.Event

    do!
      match e.Event with
      | Toxic _ -> None
      | SessionCreated e -> e |> JsonConvert.SerializeObject |> Some
      | SessionTerminated e -> e |> JsonConvert.SerializeObject |> Some
      | UserCreated e -> e |> JsonConvert.SerializeObject |> Some
      | RoleAdded e -> e |> JsonConvert.SerializeObject |> Some
      | RoleRemoved e -> e |> JsonConvert.SerializeObject |> Some
      | ChannelCreated e -> e |> JsonConvert.SerializeObject |> Some
      | ChannelEnabled e -> e |> JsonConvert.SerializeObject |> Some
      | ChannelDisabled e -> e |> JsonConvert.SerializeObject |> Some
      |> Option.map Encoding.UTF8.GetBytes
      |> Option.map (fun b -> [ EventData(Uuid.NewUuid(), etn, b) ])
      |> Option.map (fun ed -> cl.AppendToStreamAsync(sn, StreamState.Any, ed) :> Task)
      |> Option.defaultValue Task.CompletedTask
  }

let triggerSubscriptions (ports: IPorts) =
  let subscribe' = subscribe ports.configuration.EventStoreConnectionString

  let admin =
    { ID = Guid.Empty
      Email = Email ports.configuration.AdminEmail
      Roles = Roles.Admin
      Name = "Admin"
      LastLogin = None }

  do
    admin
    |> ports.save<User>
    |> fun t -> t.Result |> ignore

  let getCheckpoint cp () : Task<FromAll> =
    cp
    |> Option.map FromAll.After
    |> Option.defaultValue FromAll.Start
    |> Task.FromResult

  let mutable checkpoint: Position option = None

  let saveCheckpoint =
    Option.iter (fun p -> checkpoint <- Some p)
    >> Task.singleton

  { Handler = fun _ evnt _ -> handle evnt ports :> Task
    GetCheckpoint = getCheckpoint checkpoint
    SaveCheckpoint = saveCheckpoint }
  |> subscribe'
