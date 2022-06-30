[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Views.Channels.ListChannels

open CommunicationsManagement.API.Models
open Giraffe.ViewEngine.HtmlElements

type ChannelListViewModel = { Channels: Channel list }

let list (vm: ViewModel<ChannelListViewModel>) : XmlNode list = []
