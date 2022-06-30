[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Routing.Channels

open CommunicationsManagement.API.Routing.Routes.EffectfulRoutes
open Giraffe

let list: EffectRoute<HttpHandler> = effectRoute { return redirectTo false "/" }
