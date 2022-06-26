﻿module TestProject1CommunicationsManagement.Test.UserEdition

open CommunicationsManagement.API.Models
open Xunit
open TestSetup
open TestUtils


[<Fact>]
let ``user is edited`` () =
  task {
    use! setup = testSetup ()

    let testUser = createAndLogin (Roles.UserManagement ||| Roles.Press) setup

    login setup.config.AdminEmail setup

    failwith "test edition"
  }
