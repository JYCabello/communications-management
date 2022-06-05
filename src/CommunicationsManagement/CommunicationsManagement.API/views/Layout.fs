module CommunicationsManagement.API.views.Layout

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine

let private navTemplate (vmr: ViewModelRoot) =
  nav [] [
    yield!
      vmr.User
      |> Option.map (fun u -> a [ _href "." ] [ Text u.Name ])
      |> Option.toList
  ]

let layout (vmr: ViewModelRoot) (bodyContent: XmlNode seq) =
  html [] [
    head [] [
      title [] [
        vmr.Title
        |> (Option.defaultValue "Comunicaciones deportivas")
        |> str
      ]
    ]
    body [] [
      navTemplate vmr
      div [ _id "body-container" ] [
        yield! bodyContent
      ]
    ]
  ]
