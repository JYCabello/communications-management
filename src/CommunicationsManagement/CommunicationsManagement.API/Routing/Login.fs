[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Login

open System
open System.Threading.Tasks
open CommunicationsManagement.API
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Models.NotificationModels
open CommunicationsManagement.API.Routing.Routes
open FsToolkit.ErrorHandling
open Routes.Rendering
open Giraffe
open Models
open EffectfulRoutes
open Effects

[<CLIMutable>]
type LoginDto = { Email: string option }

let get =
  effectRoute {
    let! vmr = getAnonymousRootModel

    return!
      renderOk
        Views.Login.loginView
        { Model = { Email = None; EmailError = None }
          Root = vmr }
  }

type LoginResult =
  | Success
  | Failure of Views.Login.LoginModel

let post: EffectRoute<HttpHandler> =
  let create dto rm =
    effectRoute {
      let! user = query<User> (fun u -> u.Email = (dto.Email |> (Option.defaultValue "") |> Email))

      let session =
        { UserID = user.ID
          ID = Guid.NewGuid()
          ExpiresAt = DateTime.UtcNow.AddDays(15) }

      do!
        emit
          { Event =
              SessionCreated
                { SessionID = session.ID
                  UserID = session.UserID
                  ExpiresAt = session.ExpiresAt } }

      do!
        notify
          { Email = user.Email
            Notification =
              Login
                { UserName = user.Name
                  ActivationCode = session.ID
                  ActivationUrl = $"{rm.BaseUrl}/login/confirm?code={session.ID}" } }

      return
        renderOk
          Views.Login.loginMessage
          { Root = rm
            Model = rm.Translate "EmailLoginDetails" }
    }

  let renderErrors rm error dto =
    effectRoute {
      return
        renderOk
          Views.Login.loginView
          { Model =
              { Email = dto.Email
                EmailError = Some error }
            Root = rm }
    }

  effectRoute {
    let! dto = fromForm<LoginDto>
    let! rm = getAnonymousRootModel

    return!
      match DataValidation.validateEmail dto.Email rm.Translate with
      | None -> create dto rm
      | Some error -> renderErrors rm error dto
  }

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
    do! emit { Event = SessionTerminated { SessionID = sessionID } }
    let! mr = getAnonymousRootModel
    return! redirectTo false mr.BaseUrl
  }
