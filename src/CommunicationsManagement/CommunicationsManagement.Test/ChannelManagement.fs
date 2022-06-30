module TestProject1CommunicationsManagement.Test.``channel management``

open CommunicationsManagement.API.Models
open OpenQA.Selenium
open TestSetup
open TestUtils
open Xunit

[<Fact>]
let ``adds and disables channels`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver

    createAndLogin Roles.ChannelManagement setup
    |> ignore

    driver.Url <- setup.config.BaseUrl
    driver.FindElement(By.Id("channels-link")).Click()
    Assert.Empty(driver.FindElements(By.ClassName("enable-channel-link")))
    Assert.Empty(driver.FindElements(By.ClassName("disable-channel-link")))

    driver
      .FindElement(By.Id("new-channel-link"))
      .Click()

    driver
      .FindElement(By.Name("name"))
      .SendKeys("Brand new channel")

    driver
      .FindElement(By.Id("channel-submit"))
      .Click()

    Assert.NotEmpty(driver.FindElements(By.ClassName("disable-channel-link")))

    driver
      .FindElement(By.ClassName("disable-channel-link"))
      .Click()

    Assert.NotEmpty(driver.FindElements(By.ClassName("enable-channel-link")))

    driver
      .FindElement(By.ClassName("enable-channel-link"))
      .Click()

    Assert.NotEmpty(driver.FindElements(By.ClassName("disable-channel-link")))
  }
