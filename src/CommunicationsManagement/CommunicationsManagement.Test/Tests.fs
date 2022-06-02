module Tests

open System.Threading
open OpenQA.Selenium
open TestProject1CommunicationsManagement.Test
open Xunit
open TestSetup

// Putting the tests in different modules allows for parallelization

module A =
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

module B =
  [<Fact>]
  let ``greets again`` () =
    task {
      use! setup = testSetup ()

      for _ in [ 1..1_000 ] do
        Thread.Sleep(100)

      let driver = setup.driver
      driver.Url <- setup.baseUrl
      driver.Navigate() |> ignore
      let greeting = driver.FindElement(By.Id("greeting"))
      Assert.Equal("Hello there my friend!", greeting.Text)
      Assert.True(true)
    }
