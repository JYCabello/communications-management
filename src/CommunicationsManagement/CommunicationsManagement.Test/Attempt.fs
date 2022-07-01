module TestProject1CommunicationsManagement.Test.``attempt wrapper``

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Xunit
open Effects


[<Fact>]
let ``intercepts the error`` () =
  let blowsUp () =
    task {
      failwith "boom"
      return Ok "hello"
    }

  task {
    let! r = blowsUp () |> attempt

    (match r with
     | Ok _ -> false
     | Error e ->
       match e with
       | InternalServerError e ->
         Assert.Contains("boom", e)
         true
       | _ -> false)
    |> Assert.True
  }
