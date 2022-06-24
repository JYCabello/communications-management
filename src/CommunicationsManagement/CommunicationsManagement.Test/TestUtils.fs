module TestProject1CommunicationsManagement.Test.TestUtils

open System.Threading
open CommunicationsManagement.API.Models
open OpenQA.Selenium
open Xunit
open TestSetup
open Flurl

let login email (setup: Setup) =
  let driver = setup.driver

  Assert.Equal(setup.config.BaseUrl.AppendPathSegment("login"), driver.Url)

  driver
    .FindElement(By.Name("email"))
    .SendKeys email

  driver.FindElement(By.Id("email-sumbit")).Click()
  Thread.Sleep(200)
  let notification = setup.lastNotification
  Assert.Equal(email |> Email, notification.Email)

  let loginNotification =
    match notification.Notification with
    | Login ln -> ln
    | _ -> failwith "Should have been a login notification"

  driver.FindElement(By.Id("home-button")).Click()

  Assert.Equal(
    $"{setup.config.BaseUrl}/login/confirm?code={loginNotification.ActivationCode}",
    loginNotification.ActivationUrl
  )

  driver.Url <- loginNotification.ActivationUrl

  let link = driver.FindElement(By.Id("logout-link"))
  Assert.Equal("Logout", link.Text)
  Assert.Equal(setup.config.BaseUrl + "/", driver.Url)

let logout (setup: Setup) =
  setup.driver.Url <- setup.config.BaseUrl.AppendPathSegment("logout")
