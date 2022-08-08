[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Login

open System
open CommunicationsManagement.API
open CommunicationsManagement.API.EffectfulValidate
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.EventModels
open CommunicationsManagement.API.Models.NotificationModels
open CommunicationsManagement.API.Ports
open Routes.Rendering
open Giraffe
open EffectRouteOps


[<CLIMutable>]
type LoginDto = { Email: string option }

let get: RailRoute<HttpHandler> =
  rail {
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

let post: RailRoute<HttpHandler> =
  let accept email rm : RailRoute<HttpHandler> =
    rail {
      let! user = query<User> (fun u -> u.Email = email)

      let userID =
        match user with
        | Admin _ -> AdminUser.id
        | Regular ru -> ru.ID

      let session =
        { UserID = userID
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

      return
        renderOk
          Views.Login.loginMessage
          { Root = rm
            Model = rm.Translate "EmailLoginDetails" }
    }

  let validate (dto: LoginDto) (_: IPorts) =
    effectValidation { return! DataValidation.validateEmail (nameof dto.Email) dto.Email }

  let renderErrors dto ve : RailRoute<HttpHandler> =
    rail {
      let! rm = getAnonymousRootModel

      return
        renderOk
          Views.Login.loginView
          { Model =
              { Email = dto.Email
                EmailError = errorFor (nameof dto.Email) ve rm.Translate }
            Root = rm }
    }

  rail {
    let! dto = fromForm<LoginDto>
    let! rm = getAnonymousRootModel
    let! validationResult = validate dto

    return!
      match validationResult with
      | Valid email -> accept email rm
      | Invalid ve -> renderErrors dto ve
  }

let confirm: RailRoute<HttpHandler> =
  rail {
    let! mr = getAnonymousRootModel
    let! code = queryGuid "code"
    do! setCookie "sessionID" code
    return redirectTo false mr.BaseUrl
  }

let logout: RailRoute<HttpHandler> =
  rail {
    let! sessionID = getSessionID
    do! emit { Event = SessionTerminated { SessionID = sessionID } }
    let! loginUrl = buildUrl [ "login" ] []
    return redirectTo false loginUrl
  }
