module TestProject1CommunicationsManagement.Test.``channel management``

open System.Threading.Tasks
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
    Assert.Empty(driver.FindElements(By.CssSelector ".enable-channel-link"))
    Assert.Empty(driver.FindElements(By.CssSelector ".disable-channel-link"))
    driver |> click "#new-channel-link"
    // Just trigger validation
    driver |> click "#channel-sumbit"

    driver
      .FindElement(By.Name "name")
      .SendKeys("Brand new channel")

    driver |> click "#channel-sumbit"
    driver |> click "#close-button"
    Assert.NotEmpty(driver.FindElements(By.CssSelector ".disable-channel-link"))
    driver |> click ".disable-channel-link"
    driver |> click "#close-button"
    Assert.NotEmpty(driver.FindElements(By.CssSelector ".enable-channel-link"))
    driver |> click ".enable-channel-link"
    driver |> click "#close-button"
    Assert.NotEmpty(driver.FindElements(By.CssSelector ".disable-channel-link"))
  }
