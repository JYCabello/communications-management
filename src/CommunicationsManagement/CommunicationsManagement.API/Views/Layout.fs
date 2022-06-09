[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Layout

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine
open Flurl

let private navTemplate (vmr: ViewModelRoot) =
  let langUrls =
    [ (vmr.CurrentUrl.SetQueryParam("setLang", "en"), "en")
      (vmr.CurrentUrl.SetQueryParam("setLang", "es"), "es") ]

  nav [] [
    yield!
      langUrls
      |> List.collect (fun (url, lang) ->
        [ a [ _href (url.ToString()) ] [
            Text lang
          ]
          Text "&nbsp;" ])

    yield!
      vmr.User
      |> Option.map (fun u ->
        [ Text u.Name
          Text "&nbsp;"
          a [ _href vmr.BaseUrl ] [
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
