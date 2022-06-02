module TestProject1CommunicationsManagement.Test.Login

open Microsoft.AspNetCore.Components.Forms
open OpenQA.Selenium
open Xunit
open TestSetup

let goTo url (driver: WebDriver) =
  driver.Url <- url
  driver.Navigate()

[<Fact>]
let ``logs in successfully`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver
    let input = driver.FindElement(By.Id("email-input")) :?> InputText
    let button = driver.FindElement(By.Id("email-sumbit"))


    return ()
  }
