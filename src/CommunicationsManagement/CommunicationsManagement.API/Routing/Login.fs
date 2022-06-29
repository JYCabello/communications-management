module CommunicationsManagement.API.Routing.Login

open System
open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Routing.Routes
open FsToolkit.ErrorHandling
open Routes.Rendering
open Giraffe
open Models
open Views.Login
open EffectfulRoutes

[<CLIMutable>]
type LoginDto = { Email: string option }

let get =
  effectRoute {
    let! vmr = getAnonymousRootModel

    return!
      renderOk2
        loginView
        { Model = { Email = None; EmailError = None }
          Root = vmr }
  }

type LoginResult =
  | Success
  | Failure of LoginModel

let post (ports: IPorts) : HttpHandler =
  fun next ctx ->
    effect {
      let! dto =
        ctx.TryBindFormAsync<LoginDto>()
        |> TaskResult.mapError (fun _ -> BadRequest)

      let! rm = getAnonymousRootModel ctx

      let emailError = DataValidation.isValidEmail dto.Email rm.Translate

      // Short-circuit for validation.
      do!
        match emailError with
        | None -> Ok()
        | Some error ->
          { Model =
              { Email = dto.Email
                EmailError = Some error }
            Root = rm }
          |> fun m -> renderOk m loginView
          |> EarlyReturn
          |> Error

      let! user =
        fun p -> p.find<User> (fun u -> u.Email = (dto.Email |> (Option.defaultValue "") |> Email))

      let session =
        { UserID = user.ID
          ID = Guid.NewGuid()
          ExpiresAt = DateTime.UtcNow.AddDays(15) }

      do!
        fun p ->
          p.sendEvent
            { Event =
                SessionCreated
                  { SessionID = session.ID
                    UserID = session.UserID
                    ExpiresAt = session.ExpiresAt } }

      let! notification =
        fun p ->
          { Email = user.Email
            Notification =
              Login
                { UserName = user.Name
                  ActivationCode = session.ID
                  ActivationUrl = $"{p.configuration.BaseUrl}/login/confirm?code={session.ID}" } }
          |> TaskResult.ok

      do! fun p -> p.sendNotification rm.Translate notification

      return
        { Root = rm
          Model = rm.Translate "EmailLoginDetails" }
    }
    |> resolveEffect2 ports loginMessage next ctx

let confirm =
  effectRoute {
    let! mr = getAnonymousRootModel
    let! code = queryGuid "code"
    do! setCookie "sessionID" code
    return! redirectTo false mr.BaseUrl
  }

let logout =
  effectRoute {
    let! sessionID = getSessionID
    do! fun (p: IPorts) -> p.sendEvent { Event = SessionTerminated { SessionID = sessionID } }
    let! mr = getAnonymousRootModel
    do! Task.Delay(25)
    return! redirectTo false mr.BaseUrl
  }
