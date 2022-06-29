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

let confirm (ports: IPorts) : HttpHandler =
  fun next ctx ->
    effectRoute {
      let! mr = getAnonymousRootModel
      let! code = queryGuid "code"

      do ctx.Response.Cookies.Append("sessionID", code.ToString())

      // Short-circuit for redirection.
      do!
        redirectTo false mr.BaseUrl
        |> EarlyReturn
        |> Error

      return { Model = (); Root = mr }
    }
    |> resolveER theVoid ports next ctx

let logout (ports: IPorts) : HttpHandler =
  fun next ctx ->
    effect {
      let! sessionID = getSessionID ctx
      do! fun p -> p.sendEvent { Event = SessionTerminated { SessionID = sessionID } }
      // Short-circuit for redirection.
      let! baseUrl = fun p -> p.configuration.BaseUrl |> TaskResult.ok
      do! Task.Delay(25)
      do! redirectTo false baseUrl |> EarlyReturn |> Error
      let! mr = getAnonymousRootModel ctx
      return { Model = (); Root = mr }
    }
    |> resolveEffect2 ports theVoid next ctx
