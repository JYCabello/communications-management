module TestProject1CommunicationsManagement.Test.``Communications requests in edition``

open CommunicationsManagement.API.Models
open OpenQA.Selenium
open Xunit
open TestSetup
open TestUtils

[<Fact>]
let ``creates a communication request`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver
    createChannel "test channel for test request in edition" setup

    createAndLogin Roles.Delegate setup |> ignore

    driver |> click "#communication-requests-link"
    Assert.Empty(driver.FindElements(By.CssSelector ".details-request-link"))
    driver |> click "#new-request-link"

    driver
      .FindElement(By.Name "title")
      .SendKeys("test request in edition")

    driver |> click "#request-sumbit"
    driver |> click "#close-button"
    driver |> click ".details-request-link"

    driver.FindElement(By.Name "title")
    |> fun titleInput ->
         Assert.Equal("test request in edition", titleInput.GetAttribute "value")
         titleInput.Clear()
         titleInput.SendKeys "edited test request"

    driver.FindElement(By.Name "content")
    |> fun titleInput ->
         Assert.Equal("", titleInput.GetAttribute "value")
         titleInput.Clear()
         titleInput.SendKeys "Content of the request"

    driver |> click "#request-sumbit"
    driver |> click "#close-button"

    driver.FindElement(By.Name "title")
    |> fun titleInput -> Assert.Equal("edited test request", titleInput.GetAttribute "value")

    driver.FindElement(By.Name "content")
    |> fun contentInput -> Assert.Equal("Content of the request", contentInput.GetAttribute "value")

    failwith "pending check for file uploads"
  }
