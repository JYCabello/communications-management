[<Microsoft.FSharp.Core.RequireQualifiedAccess>]
module CommunicationsManagement.API.Option

let bindBool f =
  function
  | Some x ->
    match f x with
    | true -> Some x
    | false -> None
  | None -> None
