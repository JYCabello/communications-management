[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Layout

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open Urls

let private navTemplate (vmr: ViewModelRoot) =
  let langUrls =
    [ (urlFor vmr.CurrentUrl [] [ ("setLang", "en") ], "en")
      (urlFor vmr.CurrentUrl [] [ ("setLang", "es") ], "es") ]

  let trxTxt = vmr.Translate >> Text

  nav [] [
    a [ _href "/" ] [ "Home" |> trxTxt ]
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
          a [ _href <| urlFor vmr.BaseUrl [ "logout" ] []
              _id "logout-link" ] [
            "Logout" |> trxTxt
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
