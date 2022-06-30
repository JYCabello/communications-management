[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Channels.ListChannels

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Flurl
open Giraffe.ViewEngine

type ChannelListViewModel = { Channels: Channel list }

let list (vm: ViewModel<ChannelListViewModel>) : XmlNode list =
  let newChannelUrl =
    vm
      .Root
      .BaseUrl
      .AppendPathSegments("channels", "create")
      .ToString()

  [ div [ _class "d-flex flex-row-reverse" ] [
      a [ _href newChannelUrl
          _class "btn btn-success btn-sm user-link"
          _id "new-channel-link" ] [
        "New" |> vm.Root.Translate |> Text
      ]
    ] ]
