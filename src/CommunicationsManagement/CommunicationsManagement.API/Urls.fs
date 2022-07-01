module CommunicationsManagement.API.Urls

open Flurl

let append segment (url: string) =
  url
    .AppendPathSegment(segment.ToString())
    .ToString()

let addQueryParam name value (url: string) =
  url
    .SetQueryParam(name, value.ToString())
    .ToString()
