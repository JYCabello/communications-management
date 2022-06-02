module Tests

open OpenQA.Selenium
open TestProject1CommunicationsManagement.Test
open Xunit
open TestSetup

// Putting the tests in different modules allows for parallelization

module A =
  [<Fact>]
  let ``greets you`` () =
    task {
      use! setup = testSetup ()
      let greeting = setup.driver.FindElement(By.Id("greeting"))
      Assert.Equal("Hello there my friend!", greeting.Text)
    }

module B =
  [<Fact>]
  let ``greets again`` () =
    task {
      use! setup = testSetup ()
      let greeting = setup.driver.FindElement(By.Id("greeting"))
      Assert.Equal("Hello there my friend!", greeting.Text)
    }
