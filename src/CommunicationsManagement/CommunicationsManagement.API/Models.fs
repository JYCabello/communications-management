module CommunicationsManagement.API.Models

open System
open System.Collections.Concurrent


type Email = Email of string

type Roles =
  | None = 0
  | Delegate = 1
  | Press = 2
  | UserManagement = 4
  | ChannelManagement = 8
  | Admin = 131071

let getRoleName =
  function
  | Roles.Delegate -> "Delegate"
  | Roles.Press -> "Press"
  | Roles.UserManagement -> "UserManagement"
  | Roles.ChannelManagement -> "ChannelManagement"
  | _ -> "Unknown"

let contains (searchTerm: Roles) (userRoles: Roles) =
  (searchTerm &&& userRoles) = searchTerm
  && (userRoles = Roles.None |> not)
  && (searchTerm = Roles.None |> not)

type DomainError =
  | NotAuthenticated
  | Unauthorized of protectedResourceName: string
  | NotFound of resourceName: string
  | Conflict
  | BadRequest
  | InternalServerError of errorMessage: string

type User =
  { Name: string
    ID: Guid
    Email: Email
    Roles: Roles
    LastLogin: DateTime option }
  member this.hasRole roles = contains roles this.Roles

type Session =
  { ID: Guid
    UserID: Guid
    ExpiresAt: DateTime }

type Channel =
  { ID: Guid
    Name: string
    IsEnabled: bool }

type Media = { ID: Guid; FileName: string }

type EditingCommunicationsRequest =
  { ID: Guid
    Title: string
    Body: string
    Media: Media list }
  
type MemoryStorage =
  { Users: ConcurrentDictionary<Guid, User>
    Sessions: ConcurrentDictionary<Guid, Session>
    Channels: ConcurrentDictionary<Guid, Channel>
    EditingCommunicationsRequests: ConcurrentDictionary<Guid, EditingCommunicationsRequest> }

[<CLIMutable>]
type Configuration =
  { BlobStorageConnectionString: string
    EventStoreConnectionString: string
    BaseUrl: string
    AdminEmail: string
    SendGridKey: string
    MailFrom: string }

type Translator = string -> string

type ViewModelRoot =
  { User: User option
    Title: string
    Translate: Translator
    CurrentUrl: string
    BaseUrl: string }

type ViewModel<'a> = { Root: ViewModelRoot; Model: 'a }

module EventModels =

  [<CLIMutable>]
  type ToxicEvent = { Content: string; Type: string }

  [<CLIMutable>]
  type SessionCreated =
    { SessionID: Guid
      UserID: Guid
      ExpiresAt: DateTime }

  [<CLIMutable>]
  type SessionTerminated = { SessionID: Guid }

  [<CLIMutable>]
  type UserCreated =
    { UserID: Guid
      Email: string
      Name: string
      Roles: Roles }

  [<CLIMutable>]
  type RoleAdded = { UserID: Guid; RoleToAdd: Roles }

  [<CLIMutable>]
  type RoleRemoved = { UserID: Guid; RoleRemoved: Roles }

  [<CLIMutable>]
  type ChannelCreated =
    { ChannelID: Guid
      ChannelName: string }

  [<CLIMutable>]
  type ChannelEnabled = { ChannelID: Guid }

  [<CLIMutable>]
  type ChannelDisabled = { ChannelID: Guid }

  type StreamEvent =
    | SessionCreated of SessionCreated
    | SessionTerminated of SessionTerminated
    | UserCreated of UserCreated
    | RoleAdded of RoleAdded
    | RoleRemoved of RoleRemoved
    | ChannelCreated of ChannelCreated
    | ChannelEnabled of ChannelEnabled
    | ChannelDisabled of ChannelDisabled
    | Toxic of ToxicEvent

  let getEventTypeName =
    function
    | SessionCreated _ -> "SessionCreated"
    | SessionTerminated _ -> "SessionTerminated"
    | UserCreated _ -> "UserCreated"
    | RoleAdded _ -> "RoleAdded"
    | RoleRemoved _ -> "RoleRemoved"
    | ChannelCreated _ -> "ChannelCreated"
    | ChannelEnabled _ -> "ChannelEnabled"
    | ChannelDisabled _ -> "ChannelDisabled"
    | Toxic _ -> "Toxic"

  let getStreamName =
    function
    | SessionCreated _ -> "Sessions"
    | SessionTerminated _ -> "Sessions"
    | UserCreated _ -> "Users"
    | RoleAdded _ -> "Users"
    | RoleRemoved _ -> "Users"
    | ChannelCreated _ -> "Channels"
    | ChannelEnabled _ -> "Channels"
    | ChannelDisabled _ -> "Channels"
    | Toxic _ -> "toxic"

  type SendEventParams = { Event: StreamEvent }

module NotificationModels =
  type WelcomeNotification = { UserName: string }

  type LoginNotification =
    { UserName: string
      ActivationCode: Guid
      ActivationUrl: string }

  type Notification =
    | Welcome of WelcomeNotification
    | Login of LoginNotification

  type SendNotificationParams =
    { Notification: Notification
      Email: Email }
