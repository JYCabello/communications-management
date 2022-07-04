[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Layout

open System.Net.Mime
open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open Flurl

let private navTemplate (vmr: ViewModelRoot) =
  let langUrls =
    [ (vmr
        .CurrentUrl
        .SetQueryParam("setLang", "en")
         .ToString(),
       "en")
      (vmr
        .CurrentUrl
        .SetQueryParam("setLang", "es")
         .ToString(),
       "es") ]

  nav [] [
    a [ _href "/" ] [
      "Home" |> vmr.Translate |> Text
    ]
    Text "&nbsp;"
    yield!
      langUrls
      |> List.collect (fun (url, lang) ->
        [ a [ _href url ] [ Text lang ]
          Text "&nbsp;" ])

    yield!
      vmr.User
      |> Option.map (fun u ->
        [ span [ _id "user-name-nav" ] [
            Text u.Name
          ]
          Text "&nbsp;"
          a [ _href (vmr.BaseUrl.AppendPathSegment("logout").ToString())
              _id "logout-link" ] [
            "Logout" |> vmr.Translate |> Text
          ] ])
      |> Option.toList
      |> List.collect id
  ]

let homeButton translate =
  a [ _href "/"
      _class "btn btn-primary"
      _id "home-button" ] [
    translate "Home" |> Text
  ]

let closeButton translate url =
  a [ _href url
      _class "btn btn-primary"
      _id "close-button" ] [
    translate "Close" |> Text
  ]

let layout (vmr: ViewModelRoot) (bodyContent: XmlNode seq) =
  html [] [
    head [] [
      title [] [ Text vmr.Title ]
      link [ _href "https://cdn.jsdelivr.net/npm/bootstrap@5.2.0-beta1/dist/css/bootstrap.min.css"
             _rel "stylesheet"
             _integrity "sha384-0evHe/X+R7YkIZDRvuzKMRqM+OrBnVFBL6DOitfPri4tjfHxaWutUpFmBp4vmVor"
             _crossorigin "anonymous" ]
    ]
    body [ _class "container" ] [
      navTemplate vmr
      div [ _id "body-container" ] [
        yield! bodyContent
      ]
    ]
  ]

let notification translate bodyContent =
  html [] [
    head [] [
      title [] [
        "AppName" |> translate |> Text
      ]
      link [ _href "https://cdn.jsdelivr.net/npm/bootstrap@5.2.0-beta1/dist/css/bootstrap.min.css"
             _rel "stylesheet"
             _integrity "sha384-0evHe/X+R7YkIZDRvuzKMRqM+OrBnVFBL6DOitfPri4tjfHxaWutUpFmBp4vmVor"
             _crossorigin "anonymous" ]
    ]
    body [ _class "container" ] [
      div [ _id "body-container" ] [
        yield! bodyContent
        homeButton translate
      ]
    ]
  ]

type ReturnViewModel = { Message: string; Url: string }

let notificationReturn (vm: ViewModel<ReturnViewModel>) =
  let trx = vm.Root.Translate

  html [] [
    head [] [
      title [] [ "AppName" |> trx |> Text ]
      link [ _href "https://cdn.jsdelivr.net/npm/bootstrap@5.2.0-beta1/dist/css/bootstrap.min.css"
             _rel "stylesheet"
             _integrity "sha384-0evHe/X+R7YkIZDRvuzKMRqM+OrBnVFBL6DOitfPri4tjfHxaWutUpFmBp4vmVor"
             _crossorigin "anonymous" ]
    ]
    body [ _class "container" ] [
      div [ _id "body-container" ] [
        p [] [ vm.Model.Message |> Text ]
        closeButton trx vm.Model.Url
      ]
    ]
  ]
