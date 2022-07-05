[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Channels.ListChannels

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements
open Urls
open Giraffe.ViewEngine

type ChannelListViewModel = { Channels: Channel list }

let list (vm: ViewModel<ChannelListViewModel>) : XmlNode list =
  let trxTxt = vm.Root.Translate >> Text
  let baseUrl = vm.Root.BaseUrl

  let newChannelUrl = urlFor baseUrl [ "channels"; "create" ] []

  let enableLink (c: Channel) =
    let url = urlFor baseUrl [ "channels"; c.ID; "enable" ] []

    a [ _href url
        _class "btn btn-success btn-sm enable-channel-link" ] [
      "Enable" |> trxTxt
    ]

  let disableLink (c: Channel) =
    let url = urlFor baseUrl [ "channels"; c.ID; "disable" ] []

    a [ _href url
        _class "btn btn-danger btn-sm disable-channel-link" ] [
      "Disable" |> trxTxt
    ]

  let row (c: Channel) =
    div [ _class "row" ] [
      div [ _class "col" ] [ c.Name |> Text ]
      div [ _class "col" ] [
        match c.IsEnabled with
        | true -> "Enabled"
        | false -> "Disabled"
        |> trxTxt
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
        "New" |> trxTxt
      ]
    ]
    div [ _class "container" ] [
      div [ _class "row p-2 bd-highlight" ] [
        div [ _class "col" ] [
          "Name" |> trxTxt
        ]
        div [ _class "col" ] [
          "Status" |> trxTxt
        ]
        div [ _class "col" ] [
          "Actions" |> trxTxt
        ]
      ]
      yield! vm.Model.Channels |> List.map row
    ] ]
