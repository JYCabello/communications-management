[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Channels.ListChannels

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Flurl
open Giraffe.ViewEngine

type ChannelListViewModel = { Channels: Channel list }

let list (vm: ViewModel<ChannelListViewModel>) : XmlNode list =
  let trx = vm.Root.Translate
  let baseUrl = vm.Root.BaseUrl

  let newChannelUrl =
    baseUrl
      .AppendPathSegments("channels", "create")
      .ToString()

  let enableLink (c: Channel) =
    let enableUrl =
      baseUrl
        .AppendPathSegments("channels", c.ID, "enable")
        .ToString()

    a [ _href enableUrl
        _class "btn btn-success btn-sm enable-channel-link" ] [
      "Enable" |> trx |> Text
    ]

  let disableLink (c: Channel) =
    let enableUrl =
      baseUrl
        .AppendPathSegments("channels", c.ID, "disable")
        .ToString()

    a [ _href enableUrl
        _class "btn btn-danger btn-sm disable-channel-link" ] [
      "Disable" |> trx |> Text
    ]

  let row (c: Channel) =
    div [ _class "row" ] [
      div [ _class "col" ] [ c.Name |> Text ]
      div [ _class "col" ] [
        match c.IsEnabled with
        | true -> "Enabled"
        | false -> "Disabled"
        |> trx
        |> Text
      ]
      div [ _class "col" ] [
        match c.IsEnabled with
        | true -> disableLink c
        | false -> enableLink c
      ]
    ]

  [ div [ _class "d-flex flex-row-reverse" ] [
      a [ _href newChannelUrl
          _class "btn btn-success btn-sm user-link"
          _id "new-channel-link" ] [
        "New" |> vm.Root.Translate |> Text
      ]
    ]
    div [ _class "container" ] [
      div [ _class "row p-2 bd-highlight" ] [
        div [ _class "col" ] [
          "Name" |> vm.Root.Translate |> Text
        ]
        div [ _class "col" ] [
          "Status" |> vm.Root.Translate |> Text
        ]
        div [ _class "col" ] [
          "Actions" |> vm.Root.Translate |> Text
        ]
      ]
      yield! vm.Model.Channels |> List.map row
    ] ]
