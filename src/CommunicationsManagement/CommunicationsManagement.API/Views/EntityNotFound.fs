[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.EntityNotFound

open System
open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements

let view (t: Translator) (entityName: string) : XmlNode =
  div [] [
    Text(String.Format((t "NotFoundTextTemplate"), entityName))
  ]
