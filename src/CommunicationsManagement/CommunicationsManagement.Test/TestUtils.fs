module TestProject1CommunicationsManagement.Test.TestUtils

open System
open System.Threading
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.NotificationModels
open OpenQA.Selenium
open Xunit
open TestSetup
open Flurl

let logout (setup: Setup) =
  setup.driver.Url <-
    setup
      .config
      .BaseUrl
      .AppendPathSegment("logout")
      .ToString()

let login email (setup: Setup) =
  logout setup

  let driver = setup.driver

  Assert.Equal(
    setup
      .config
      .BaseUrl
      .AppendPathSegment("login")
      .ToString(),
    driver.Url
  )

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

type TestUserCreation = { Email: string; Name: string }

let createAndLogin (roles: Roles) (setup: Setup) =
  let testUser =
    { Email = $"{Guid.NewGuid()}@testemail.com"
      Name = Guid.NewGuid().ToString() }

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

  // Just trigger the validation.
  driver
    .FindElement(By.Id("create-user-sumbit"))
    .Click()

  driver
    .FindElement(By.Id("input-name"))
    .SendKeys(testUser.Name)

  driver
    .FindElement(By.Id("input-email"))
    .SendKeys(testUser.Email)

  let roles =
    Enum.GetValues<Roles>()
    |> Seq.filter (fun r -> contains r roles)

  let roleInputs =
    driver.FindElements(By.Name("roles"))
    |> Seq.filter (fun e ->
      roles
      |> Seq.exists (fun r -> e.GetAttribute("value") = (r |> int |> string)))

  // Verify that all roles have an input.
  Assert.Equal(roles |> Seq.length, roleInputs |> Seq.length)

  roleInputs |> Seq.iter (fun i -> i.Click())

  driver
    .FindElement(By.Id("create-user-sumbit"))
    .Click()

  login testUser.Email setup
  testUser
