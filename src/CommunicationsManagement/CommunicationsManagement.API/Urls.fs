module CommunicationsManagement.API.Urls

open Flurl


let urlFor (baseUrl: string) (segments: string seq) (queryParams: (string * string) seq)  =
  (baseUrl |> Url, segments)
  ||> Seq.fold (fun url s -> s |> url.AppendPathSegment)
  |> fun url -> (url, queryParams)
  ||> Seq.fold (fun url (key, value) -> url.SetQueryParam(key, value))
  |> string
