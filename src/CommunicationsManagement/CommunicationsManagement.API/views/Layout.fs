module CommunicationsManagement.API.views.Layout

open Giraffe.ViewEngine

let layout bodyContent = html [] [ yield! bodyContent ]
