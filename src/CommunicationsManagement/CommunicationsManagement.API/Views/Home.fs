[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Home

open CommunicationsManagement.API.Models
open CommunicationsManagement.API
open Giraffe.ViewEngine
open Urls


let private usersRow vm =
  let trxTxt = vm.Root.Translate >> Text

  vm.Root.User
  |> Option.bindBool (fun u -> u.hasRole Roles.UserManagement)
  |> Option.map (fun _ ->
    [ div [] [
        h1 [] [ "Users" |> trxTxt ]
        a [ _href <| urlFor vm.Root.BaseUrl [ "users" ] []
            _class "btn btn-primary"
            _id "users-link" ] [
          "UserManagement" |> trxTxt
        ]
      ] ])
  |> Option.toList
  |> List.collect id

let private channelsRow vm =
  let trxTxt = vm.Root.Translate >> Text

  vm.Root.User
  |> Option.bindBool (fun u -> u.hasRole Roles.ChannelManagement)
  |> Option.map (fun _ ->
    [ div [] [
        h1 [] [ "Channels" |> trxTxt ]
        a [ _href <| urlFor vm.Root.BaseUrl [ "channels" ] []
            _class "btn btn-primary"
            _id "channels-link" ] [
          "ChannelManagement" |> trxTxt
        ]
      ] ])
  |> Option.toList
  |> List.collect id

let private requestsRow vm =
  let trxTxt = vm.Root.Translate >> Text

  vm.Root.User
  |> Option.bindBool (fun u -> u.hasRole Roles.Delegate || u.hasRole Roles.Press)
  |> Option.map (fun _ ->
    [ div [] [
        h1 [] [
          "CommunicationRequests" |> trxTxt
        ]
        a [ _href
            <| urlFor vm.Root.BaseUrl [ "communication-requests" ] []
            _class "btn btn-primary"
            _id "communication-requests-link" ] [
          "Management" |> trxTxt
        ]
      ] ])
  |> Option.toList
  |> List.collect id

let homeView (vm: ViewModel<unit>) : XmlNode list =
  [ yield! usersRow vm
    yield! channelsRow vm
    yield! requestsRow vm ]
