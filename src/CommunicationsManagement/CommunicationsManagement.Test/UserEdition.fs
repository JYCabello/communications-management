module TestProject1CommunicationsManagement.Test.UserEdition

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
    driver.FindElement(By.Id("users-link")).Click()

    login setup.config.AdminEmail setup

    driver.FindElement(By.Id("users-link")).Click()

    driver
      .FindElement(By.CssSelector($"[data-user-details=\"{testUser.Email}\"]"))
      .Click()

    driver
      .FindElement(By.CssSelector("a#remove-role-4"))
      .Click()

    login testUser.Email setup

    Assert.Empty(driver.FindElements(By.Id("users-link")))

    login setup.config.AdminEmail setup

    driver.FindElement(By.Id("users-link")).Click()

    driver
      .FindElement(By.CssSelector($"a[data-user-details=\"{testUser.Email}\"]"))
      .Click()

    driver
      .FindElement(By.CssSelector("a#add-role-4"))
      .Click()

    login testUser.Email setup

    driver.FindElement(By.Id("users-link")).Click()
  }
