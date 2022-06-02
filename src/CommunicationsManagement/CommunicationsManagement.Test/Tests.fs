module Tests

open OpenQA.Selenium
open TestProject1CommunicationsManagement.Test
open Xunit
open TestSetup


[<Fact>]
let ``greets`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver
    driver.Url <- setup.baseUrl
    driver.Navigate() |> ignore
    let greeting = driver.FindElement(By.Id("greeting"))
    Assert.Equal("Hello there my friend!", greeting.Text)
    Assert.True(true)
  }
