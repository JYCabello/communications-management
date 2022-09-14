module CommunicationsManagement.API.Views.CommunicationRequests.CreateRequest

open CommunicationsManagement.API
open Models
open Views.Utils
open Giraffe.ViewEngine

type RequestCreationViewModel =
  { Title: string option
    TitleError: string option }

let create (vm: ViewModel<RequestCreationViewModel>) : XmlNode list =
  let trx = vm.Root.Translate
  let m = vm.Model

  [ h1 [] [ "CreateCommunicationsRequest" |> trx |> Text ]
    form [ _action "/communication-requests/create"
           _method "post"
           _novalidate ] [
      yield! inputGroupFor trx m.Title m.TitleError "title" "Title"
      input [ _type "submit"
              _id "request-sumbit"
              _class "btn btn-primary" ]
    ] ]
