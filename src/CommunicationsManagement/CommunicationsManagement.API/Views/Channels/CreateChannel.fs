[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Channels.CreateChannel

open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Views.Utils
open Giraffe.ViewEngine

type ChannelCreationViewModel =
  { Name: string option
    NameError: string option }

let create (vm: ViewModel<ChannelCreationViewModel>) =
  let trx = vm.Root.Translate
  let m = vm.Model

  [ h1 [] [ "CreateChannel" |> trx |> Text ]
    form [ _action "/channels/create"
           _method "post"
           _novalidate ] [
      yield! inputGroupFor trx m.Name m.NameError "name" "Name"
      input [ _type "submit"
              _id "channel-sumbit"
              _class "btn btn-primary" ]
    ] ]
