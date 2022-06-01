module Tests

open TestProject1CommunicationsManagement.Test
open Xunit
open TestContainers


[<Fact>]
let ``My test`` () =
  task {
    do! startEventStore "sometestcontainer"
    Assert.True(true)
  }
