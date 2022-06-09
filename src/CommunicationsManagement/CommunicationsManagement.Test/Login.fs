module TestProject1CommunicationsManagement.Test.Login

open System.Threading.Tasks
open CommunicationsManagement.API.Models
open OpenQA.Selenium
open Xunit
open TestSetup

[<Fact>]
let ``logs in successfully and then logs out`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver

    TestUtils.login setup.config.AdminEmail setup


    let link = driver.FindElement(By.Id("logout-link"))
    Assert.Equal("Logout", link.Text)
    Assert.Equal(setup.config.BaseUrl + "/", driver.Url)
    do link.Click()
    do! Task.Delay(100)
    Assert.Equal($"{setup.config.BaseUrl}/login", driver.Url)
  }
