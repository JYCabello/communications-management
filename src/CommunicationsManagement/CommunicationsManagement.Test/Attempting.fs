module TestProject1CommunicationsManagement.Test.Attempting

open CommunicationsManagement.API
open CommunicationsManagement.API.Models
open Xunit
open Effects

let blowsUp () =
  task {
    failwith "boom"
    return Ok "hello"
  }

[<Fact>]
let ``intercepts the error`` () =
  task {
    let! r = blowsUp () |> attempt

    (match r with
     | Ok _ -> false
     | Error e ->
       match e with
       | InternalServerError _ -> true
       | _ -> false)
    |> Assert.True
  }
