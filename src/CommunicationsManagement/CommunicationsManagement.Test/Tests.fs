module Tests

open System.Threading
open TestProject1CommunicationsManagement.Test
open Xunit
open TestSetup


[<Fact>]
let ``My test`` () =
  task {
    use! __ = testSetup ()
    Thread.Sleep(3_000)
    Assert.True(true)
  }
