module CommunicationsManagement.API.Views.CommunicationRequests.ListRequests

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements

type ListRequestViewModel =
  { InEdition: EditingCommunicationsRequest list }

let list (vm: ViewModel<ListRequestViewModel>) : XmlNode list = []
