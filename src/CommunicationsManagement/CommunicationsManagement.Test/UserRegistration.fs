﻿module TestProject1CommunicationsManagement.Test.UserRegistration

open OpenQA.Selenium
open Xunit
open TestSetup
open TestUtils


[<Fact>]
let ``registers a user`` () =
  task {
    use! setup = testSetup ()
    login setup.config.AdminEmail setup

    let driver = setup.driver
    driver.FindElement(By.Id("users-link")).Click()
    Assert.True(driver.Url.EndsWith("users"))

    Assert.Equal(
      1,
      driver
        .FindElements(
          By.ClassName("user-link")
        )
        .Count
    )

    driver
      .FindElement(By.Id("new-user-button"))
      .Click()

    driver
      .FindElement(By.Id("name-input"))
      .SendKeys("Mr Don")

    driver
      .FindElement(By.Id("email-input"))
      .SendKeys("emailio@email.com")

    driver.FindElements(By.Name("roles"))
    |> Seq.find (fun e -> e.GetAttribute("value") = "2")
    |> fun e -> e.Click()

    driver.FindElement(By.ClassName("submit")).Click()
    logout setup

    login "emailio@email.com" setup
    Assert.Equal("Mr Don", driver.FindElement(By.Id("user-name-nav")).Text)
  }