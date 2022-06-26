module TestProject1CommunicationsManagement.Test.UserEdition

open CommunicationsManagement.API.Models
open OpenQA.Selenium
open Xunit
open TestSetup
open TestUtils


[<Fact>]
let ``user is edited`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver
    let testUser = createAndLogin (Roles.UserManagement ||| Roles.Press) setup

    login setup.config.AdminEmail setup

    driver.FindElement(By.Id("users-link")).Click()
    
    


    failwith "test edition"
  }
