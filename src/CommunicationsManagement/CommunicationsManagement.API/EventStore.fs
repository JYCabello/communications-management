[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.EventStore

open System
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
    | t -> StreamEvent.Toxic { Type = t; Content = decoded }
  with
  | _ -> StreamEvent.Toxic { Type = "Message"; Content = decoded }

let private ignoreErrors =
  Task.map (fun r ->
    match r with
    | Ok u -> u
    | Error _ -> () // Ignore errors for now
  )

let private handleSession (se: ResolvedEvent) (ports: IPorts) : Task<unit> =
  let handleCreated (sc: SessionCreated) =
    taskResult {
      let! user = ports.query<User> sc.UserID
      do! ports.save<User> { user with LastLogin = DateTime.UtcNow |> Some }

      do!
        ports.save<Session>
          { ID = sc.SessionID
            UserID = sc.UserID
            ExpiresAt = sc.ExpiresAt }
    }

  let handleTerminated (st: SessionTerminated) = ports.delete<Session> st.SessionID

  match deserialize se with
  | SessionCreated sc -> handleCreated sc |> ignoreErrors
  | SessionTerminated st -> handleTerminated st |> ignoreErrors
  | _ -> Task.FromResult()

let private handleUsers (se: ResolvedEvent) (ports: IPorts) : Task<unit> =
  let handleCreated (uc: UserCreated) =
    taskResult {
      do!
        ports.save<User>
          { Name = uc.Name
            Email = uc.Email |> Email
            ID = uc.UserID
            Roles = uc.Roles
            LastLogin = None }
    }

  match deserialize se with
  | UserCreated uc -> handleCreated uc |> ignoreErrors
  | _ -> Task.FromResult()


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

    return!
      match e.Event with
      | Toxic _ -> None
      | SessionCreated e -> e |> JsonConvert.SerializeObject |> Some
      | SessionTerminated e -> e |> JsonConvert.SerializeObject |> Some
      | UserCreated e -> e |> JsonConvert.SerializeObject |> Some
      |> Option.map Encoding.UTF8.GetBytes
      |> Option.map (fun b -> EventData(Uuid.NewUuid(), eventTypeName, b))
      |> Option.map (fun ed ->
        client.AppendToStreamAsync(streamName, StreamState.Any, [ ed ]) :> Task)
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
