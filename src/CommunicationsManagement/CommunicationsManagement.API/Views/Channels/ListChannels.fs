[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Channels.ListChannels

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Urls
open Giraffe.ViewEngine

type ChannelListViewModel = { Channels: Channel list }

let list (vm: ViewModel<ChannelListViewModel>) : XmlNode list =
  let trx = vm.Root.Translate
  let baseUrl = vm.Root.BaseUrl

  let newChannelUrl = urlFor baseUrl ["channels"; "create"] []

  let enableLink (c: Channel) =
    let url = urlFor baseUrl ["channels"; c.ID |> string; "enable"] []

    a [ _href url
        _class "btn btn-success btn-sm enable-channel-link" ] [
      "Enable" |> trx |> Text
    ]

  let disableLink (c: Channel) =
    let url = urlFor baseUrl ["channels"; c.ID |> string; "disable"] []

    a [ _href url
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
