module TestProject1CommunicationsManagement.Test.TestUtils

open System
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

let createAndLogin (email: string) (userName: string) (roles: Roles) (setup: Setup) =
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
    .FindElement(By.Id("input-name"))
    .SendKeys(userName)

  driver
    .FindElement(By.Id("input-email"))
    .SendKeys(email)

  for role in
    Enum.GetValues<Roles>()
    |> Seq.filter (fun r -> contains r roles) do
    driver.FindElements(By.Name("roles"))
    |> Seq.tryFind (fun e -> e.GetAttribute("value") = (role |> int |> string))
    |> Option.iter (fun e -> e.Click())

  driver
    .FindElement(By.Id("create-user-sumbit"))
    .Click()

  logout setup

  login email setup
