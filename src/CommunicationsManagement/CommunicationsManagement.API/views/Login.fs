module CommunicationsManagement.API.views.Login

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine

let login (vm: ViewModel<string>) = [ form [] [ input [ _name "email" ] ] ]
