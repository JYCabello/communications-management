module CommunicationsManagement.API.Views.Layout

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
      link [ _href "https://cdn.jsdelivr.net/npm/bootstrap@5.2.0-beta1/dist/css/bootstrap.min.css"
             _rel "stylesheet"
             _integrity "sha384-0evHe/X+R7YkIZDRvuzKMRqM+OrBnVFBL6DOitfPri4tjfHxaWutUpFmBp4vmVor"
             _crossorigin "anonymous" ]
    ]
    body [] [
      navTemplate vmr
      div [ _id "body-container" ] [
        yield! bodyContent
      ]
    ]
  ]
