module CommunicationsManagement.API.Views.CommunicationRequests.ListRequests

open CommunicationsManagement.API
open Models
open Giraffe.ViewEngine.HtmlElements
open Urls
open Giraffe.ViewEngine

type ListRequestViewModel =
  { InEdition: EditingCommunicationsRequest list }

let list (vm: ViewModel<ListRequestViewModel>) : XmlNode list =
  let trxTxt = vm.Root.Translate >> Text
  let baseUrl = vm.Root.BaseUrl

  let newCommReqUrl = urlFor baseUrl [ "communication-requests"; "create" ] []

  [ div [ _class "d-flex flex-row-reverse" ] [
      a [ _href newCommReqUrl
          _class "btn btn-success btn-sm user-link"
          _id "new-request-link" ] [
        "New" |> trxTxt
      ]
    ] ]
