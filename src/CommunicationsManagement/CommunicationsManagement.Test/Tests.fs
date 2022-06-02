module Tests

open System.Threading
open System.Threading.Tasks
open TestProject1CommunicationsManagement.Test
open Xunit
open TestSetup


[<Fact>]
let ``My test`` () =
  task {
    use! __ = testSetup ()
    use host = Main.buildHost ()
    do! host.StartAsync()

    for _ in [ 1..100 ] do
      Thread.Sleep(150)

    Assert.True(true)
  }
