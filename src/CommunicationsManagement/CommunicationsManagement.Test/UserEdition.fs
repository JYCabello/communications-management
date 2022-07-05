module TestProject1CommunicationsManagement.Test.``user edition``

open CommunicationsManagement.API.Models
open OpenQA.Selenium
open Xunit
open TestSetup
open TestUtils


[<Fact>]
let ``can add and remove roles`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver
    let testUser = createAndLogin (Roles.UserManagement ||| Roles.Press) setup
    driver |> click "#users-link"
    login setup.config.AdminEmail setup
    driver |> click "#users-link"

    driver
    |> click $"[data-user-details=\"{testUser.Email}\"]"

    driver |> click "a#remove-role-4"
    driver |> click "#close-button"
    Assert.Empty(driver.FindElements(By.CssSelector "a#remove-role-4"))
    login testUser.Email setup
    Assert.Empty(driver.FindElements(By.Id("users-link")))
    login setup.config.AdminEmail setup
    driver |> click "#users-link"

    driver
    |> click $"a[data-user-details=\"{testUser.Email}\"]"

    driver |> click "a#add-role-4"
    driver |> click "#close-button"
    Assert.Empty(driver.FindElements(By.CssSelector "a#add-role-4"))
    login testUser.Email setup
    driver |> click "#users-link"
  }
