[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Login

open System
open CommunicationsManagement.API
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Models.NotificationModels
open CommunicationsManagement.API.Routing.Routes
open Routes.Rendering
open Giraffe
open EffectfulRoutes
open EffectOps


[<CLIMutable>]
type LoginDto = { Email: string option }

let get: EffectRoute<HttpHandler> =
  effect {
    let! vmr = getAnonymousRootModel
    return
      renderOk
        Views.Login.loginView
        { Model = { Email = None; EmailError = None }
          Root = vmr }
  }

type LoginResult =
  | Success
  | Failure of Views.Login.LoginModel

let post =
  let create email rm =
    effectRoute {
      let! user = query<User> (fun u -> u.Email = email)

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

      let! activationUrl =
        buildUrl [ "login"; "confirm" ] [
          ("code", session.ID)
        ]

      do!
        notify
          { Email = user.Email
            Notification =
              Login
                { UserName = user.Name
                  ActivationCode = session.ID
                  ActivationUrl = activationUrl } }

      return!
        renderOk
          Views.Login.loginMessage
          { Root = rm
            Model = rm.Translate "EmailLoginDetails" }
    }

  let validate (dto: LoginDto) (_: IPorts) : TaskEffectValidateResult<Email> =
    effectValidation { return! DataValidation.validateEmail (nameof dto.Email) dto.Email }

  let renderErrors dto ve =
    effectRoute {
      let! rm = getAnonymousRootModel

      return
        renderOk
          Views.Login.loginView
          { Model =
              { Email = dto.Email
                EmailError = errorFor (nameof dto.Email) ve rm.Translate }
            Root = rm }
    }

  effectRoute {
    let! dto = fromForm<LoginDto>
    let! rm = getAnonymousRootModel
    let! validationResult = validate dto

    return!
      match validationResult with
      | Valid email -> create email rm
      | Invalid ve -> renderErrors dto ve
  }

let confirm: EffectRoute<HttpHandler> =
  effect {
    let! mr = getAnonymousRootModel
    let! code = queryGuid "code"
    do! setCookie "sessionID" code
    return redirectTo false mr.BaseUrl
  }

let logout =
  effectRoute {
    let! sessionID = getSessionID
    do! emit { Event = SessionTerminated { SessionID = sessionID } }
    let! loginUrl = buildUrl [ "login" ] []
    return! redirectTo false loginUrl
  }
