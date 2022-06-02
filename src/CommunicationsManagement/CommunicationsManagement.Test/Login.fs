﻿module TestProject1CommunicationsManagement.Test.Login

open CommunicationsManagement.API.Models
open OpenQA.Selenium
open Xunit
open TestSetup

[<Fact>]
let ``logs in successfully`` () =
  task {
    use! setup = testSetup ()
    let driver = setup.driver

    driver
      .FindElement(By.Id("email-input"))
      .SendKeys setup.config.AdminEmail

    driver.FindElement(By.Id("email-sumbit")).Click()

    let notification = setup.lastNotification
    Assert.Equal(setup.config.AdminEmail |> Email, notification.Email)

    let loginNotification =
      match notification.Notification with
      | Login ln -> ln
      | _ -> failwith "Should have been a login notification"

    Assert.Equal(
      $"{setup.config.BaseUrl}/login/confirm?code={loginNotification.ActivationCode}",
      loginNotification.ActivationUrl
    )

    driver.Url <- loginNotification.ActivationUrl

    let link = driver.FindElement(By.Id("profile-link"))
    Assert.Equal("Admin", link.Text)
    Assert.Equal(setup.config.BaseUrl, driver.Url)
  }
