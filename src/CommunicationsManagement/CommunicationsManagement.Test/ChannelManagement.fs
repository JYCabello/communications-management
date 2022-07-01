module TestProject1CommunicationsManagement.Test.``channel management``

open CommunicationsManagement.API.Models
open OpenQA.Selenium
open TestSetup
open TestUtils
open Xunit

[<Fact>]
let ``adds, enables and disables channels`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver

    createAndLogin Roles.ChannelManagement setup
    |> ignore

    driver.Url <- setup.config.BaseUrl
    driver.FindElement(By.Id("channels-link")).Click()
    Assert.Empty(driver.FindElements(By.CssSelector(".enable-channel-link")))
    Assert.Empty(driver.FindElements(By.CssSelector(".disable-channel-link")))

    driver
      .FindElement(By.Id("new-channel-link"))
      .Click()    
    
    // Just trigger validation
    driver
      .FindElement(By.Id("channel-sumbit"))
      .Click()

    driver
      .FindElement(By.Name("name"))
      .SendKeys("Brand new channel")

    driver
      .FindElement(By.Id("channel-sumbit"))
      .Click()

    Assert.NotEmpty(driver.FindElements(By.CssSelector(".disable-channel-link")))

    driver
      .FindElement(By.CssSelector(".disable-channel-link"))
      .Click()

    Assert.NotEmpty(driver.FindElements(By.CssSelector(".enable-channel-link")))

    driver
      .FindElement(By.CssSelector(".enable-channel-link"))
      .Click()

    Assert.NotEmpty(driver.FindElements(By.CssSelector(".disable-channel-link")))
  }
