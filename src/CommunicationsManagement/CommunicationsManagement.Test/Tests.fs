module Tests

open System.Threading
open TestProject1CommunicationsManagement.Test
open Xunit
open TestSetup


[<Fact>]
let ``My test`` () =
  task {
    use! __ = testSetup ()

    for _ in [1..10] do
      Thread.Sleep(150)

    Assert.True(true)
  }
