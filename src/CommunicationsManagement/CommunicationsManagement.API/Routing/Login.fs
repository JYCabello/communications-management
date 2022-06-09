﻿module CommunicationsManagement.API.Routing.Login

open System
open CommunicationsManagement.API
open CommunicationsManagement.API.Effects
open FsToolkit.ErrorHandling
open Routes.Rendering
open Giraffe
open Models
open Views.Login

[<CLIMutable>]
type LoginDto = { Email: string option }

let get (ports: IPorts) : HttpHandler =
  fun next ctx ->
    effect {
      let! vmr = getAnonymousRootModel ctx

      return
        { Model = { Email = None; EmailError = None }
          Root = vmr }
    }
    |> resolveEffect2 ports loginView next ctx

type LoginResult =
  | Success
  | Failure of LoginModel

let post (ports: IPorts) : HttpHandler =
  fun next ctx ->
    effect {
      let! dto =
        ctx.TryBindFormAsync<LoginDto>()
        |> TaskResult.mapError (fun _ -> BadRequest)
        |> fromTR

      let! rm = getAnonymousRootModel ctx

      let emailError =
        match DataValidation.isValidEmail dto.Email with
        | true -> None
        | false -> "InvalidEmail" |> rm.Translate |> Some

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
          ID = Guid.NewGuid() }

      do! fun p -> p.save session

      let! notification =
        fun p ->
          { Email = user.Email
            Notification =
              Login
                { UserName = user.Name
                  ActivationCode = session.ID
                  ActivationUrl = $"{p.configuration.BaseUrl}/login/confirm?code={session.ID}" } }
          |> TaskResult.ok

      do! fun p -> p.sendNotification notification

      return
        { Root = rm
          Model = rm.Translate "EmailLoginDetails" }
    }
    |> resolveEffect2 ports loginMessage next ctx

let confirm (ports: IPorts) : HttpHandler =
  fun next ctx ->
    effect {
      let! mr = getAnonymousRootModel ctx

      let! code =
        fun _ ->
          ctx.TryGetQueryStringValue("code")
          |> Option.bind (fun c ->
            match Guid.TryParse c with
            | true, guid -> Some guid
            | false, _ -> None)
          |> function
            | Some c -> TaskResult.ok c
            | None -> TaskResult.error BadRequest

      do ctx.Response.Cookies.Append("sessionID", code.ToString())

      // Short-circuit for redirection.
      let! baseUrl = fun p -> p.configuration.BaseUrl |> TaskResult.ok
      do! redirectTo false baseUrl |> EarlyReturn |> Error

      return { Model = (); Root = mr }
    }
    |> resolveEffect2 ports theVoid next ctx

let logout (ports: IPorts) : HttpHandler =
  fun next ctx ->
    effect {
      let! sessionID = getSessionID ctx
      do! fun p -> p.delete<Session> sessionID
      // Short-circuit for redirection.
      let! baseUrl = fun p -> p.configuration.BaseUrl |> TaskResult.ok
      do! redirectTo false baseUrl |> EarlyReturn |> Error
      let! mr = getAnonymousRootModel ctx
      return { Model = (); Root = mr }
    }
    |> resolveEffect2 ports theVoid next ctx
