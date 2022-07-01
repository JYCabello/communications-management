[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.EventStore

open System
open System.Text
open System.Threading
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open EventStore.Client
open Newtonsoft.Json
open FsToolkit.ErrorHandling


type SubscriptionDetails =
  { StreamID: string
    Handler: StreamSubscription -> ResolvedEvent -> CancellationToken -> Task
    GetCheckpoint: unit -> Task<FromStream>
    SaveCheckpoint: StreamPosition -> Task }

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
let private ignoreErrors =
  Task.map (fun r ->
    match r with
    | Ok u -> u
    | Error _ -> ())

let private handleSession (se: ResolvedEvent) (ports: IPorts) : Task<unit> =
  let handleCreated (sc: SessionCreated) =
    effect {
      let! user = find<User> sc.UserID
      do! save<User> { user with LastLogin = se.OriginalEvent.Created |> Some }

      do!
        save<Session>
          { ID = sc.SessionID
            UserID = sc.UserID
            ExpiresAt = sc.ExpiresAt }
    }

  let handleTerminated (st: SessionTerminated) = delete<Session> st.SessionID

  match deserialize se with
  | SessionCreated sc -> handleCreated sc
  | SessionTerminated st -> handleTerminated st
  | _ -> effect { return () }
  |> solve ports
  |> ignoreErrors

let private handleUsers (se: ResolvedEvent) (ports: IPorts) : Task<unit> =
  let handleCreated (uc: UserCreated) =
    save<User>
      { Name = uc.Name
        Email = uc.Email |> Email
        ID = uc.UserID
        Roles = uc.Roles
        LastLogin = None }

  let handleRoleAdded (ra: RoleAdded) =
    effect {
      let! user = find<User> ra.UserID
      do! save<User> { user with Roles = user.Roles + ra.RoleToAdd }
    }

  let handleRoleRemoved (rr: RoleRemoved) =
    effect {
      let! user = find<User> rr.UserID
      do! save<User> { user with Roles = user.Roles - rr.RoleRemoved }
    }

  match deserialize se with
  | UserCreated uc -> handleCreated uc
  | RoleAdded ra -> handleRoleAdded ra
  | RoleRemoved rr -> handleRoleRemoved rr
  | _ -> effect { return () }
  |> solve ports
  |> ignoreErrors


let private handleChannel (se: ResolvedEvent) (ports: IPorts) : Task<unit> =
  let handleCreated (e: ChannelCreated) =
    save<Channel>
      { ID = e.ChannelID
        Name = e.ChannelName
        IsEnabled = true }

  let handleEnabled (e: ChannelEnabled) =
    effect {
      let! channel = find<Channel> e.ChannelID
      do! save { channel with IsEnabled = true }
    }

  let handleDisabled (e: ChannelDisabled) =
    effect {
      let! channel = find<Channel> e.ChannelID
      do! save { channel with IsEnabled = false }
    }

  match deserialize se with
  | ChannelCreated cc -> handleCreated cc
  | ChannelEnabled ce -> handleEnabled ce
  | ChannelDisabled cd -> handleDisabled cd
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
              do! subscription.SaveCheckpoint e.OriginalEventNumber
            }

        let client = getClient cs

        do!
          client.SubscribeToStreamAsync(
            subscription.StreamID,
            checkpoint,
            handler,
            false,
            reSubscribe
          )
          :> Task
      with
      | _ ->
        do! Task.Delay(75)
        return! subscribeTo ()
    }

  subscribeTo () |> ignore

let sendEvent (c: Configuration) (e: SendEventParams) : Task<Result<unit, DomainError>> =
  taskResult {
    let client = getClient c.EventStoreConnectionString
    let streamName = getStreamName e.Event
    let eventTypeName = getEventTypeName e.Event

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
      |> Option.map (fun b -> EventData(Uuid.NewUuid(), eventTypeName, b))
      |> Option.map (fun ed ->
        client.AppendToStreamAsync(streamName, StreamState.Any, [ ed ]) :> Task)
      |> Option.defaultValue Task.CompletedTask
    
    // Give the event processor a bit of space to process the message.
    // This is work in progress I probably need to redesign the flow around it.
    do! Task.Delay(25)
  }

let triggerSubscriptions (ports: IPorts) =
  let subscribe' = subscribe ports.configuration.EventStoreConnectionString

  let admin =
    { ID = Guid.Empty
      Email = Email ports.configuration.AdminEmail
      Roles = Roles.Admin
      Name = "Admin"
      LastLogin = None }

  do ports.save admin |> fun t -> t.Result |> ignore

  let getCheckpoint cp () : Task<FromStream> =
    cp
    |> Option.map FromStream.After
    |> Option.defaultValue FromStream.Start
    |> Task.FromResult

  let mutable usersCheckpoint: StreamPosition option = None

  let saveUsersCheckpoint p =
    usersCheckpoint <- Some p
    Task.CompletedTask

  { StreamID = "Users"
    Handler = fun _ evnt _ -> task { do! handleUsers evnt ports } :> Task
    GetCheckpoint = getCheckpoint usersCheckpoint
    SaveCheckpoint = saveUsersCheckpoint }
  |> subscribe'

  let mutable sessionsCheckpoint: StreamPosition option = None

  let saveSessionCheckpoint p =
    sessionsCheckpoint <- Some p
    Task.CompletedTask

  { StreamID = "Sessions"
    Handler = fun _ evnt _ -> task { do! handleSession evnt ports } :> Task
    GetCheckpoint = getCheckpoint sessionsCheckpoint
    SaveCheckpoint = saveSessionCheckpoint }
  |> subscribe'

  let mutable channelsCheckpoint: StreamPosition option = None

  let saveChannelsCheckpoint p =
    channelsCheckpoint <- Some p
    Task.CompletedTask

  { StreamID = "Channels"
    Handler = fun _ evnt _ -> task { do! handleChannel evnt ports } :> Task
    GetCheckpoint = getCheckpoint channelsCheckpoint
    SaveCheckpoint = saveChannelsCheckpoint }
  |> subscribe'
