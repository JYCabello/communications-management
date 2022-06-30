module TestProject1CommunicationsManagement.Test.TestSetup

open System
open System.Collections.Generic
open System.Net.NetworkInformation
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
open CommunicationsManagement.API.Models.NotificationModels
open Docker.DotNet
open Docker.DotNet.Models
open FsToolkit.ErrorHandling
open OpenQA.Selenium
open OpenQA.Selenium.Firefox

type Setup
  (
    disposers: (unit -> unit) list,
    getLastNotification: unit -> SendNotificationParams,
    webDriver: WebDriver,
    ports: IPorts
  ) =

  member this.lastNotification = getLastNotification ()

  member this.driver = webDriver

  member this.config = ports.configuration

  interface IDisposable with
    member this.Dispose() =
      for disposer in disposers do
        disposer ()

let getDockerClient () =
  let defaultWindowsDockerEngineUri = Uri("npipe://./pipe/docker_engine")

  let defaultLinuxDockerEngineUri =
    match Environment.GetEnvironmentVariable("DOCKER_HOST") with
    | null -> Uri("unix:///var/run/docker.sock")
    | value -> Uri(value)

  let engineUri =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
      defaultLinuxDockerEngineUri
    else
      defaultWindowsDockerEngineUri

  (new DockerClientConfiguration(engineUri))
    .CreateClient()

let private deleteContainer id =
  task {
    use client = getDockerClient ()
    do! client.Containers.StopContainerAsync(id, ContainerStopParameters()) :> Task
    do! client.Containers.RemoveContainerAsync(id, ContainerRemoveParameters())
  }

let private startContainer (cp: CreateContainerParameters) =
  task {
    use client = getDockerClient ()

    let! containers =
      client.Containers.ListContainersAsync(
        ContainersListParameters()
        |> fun p ->
             p.All <- true
             p
      )

    let container =
      containers
      |> Seq.tryFind (fun c -> c.Names |> Seq.exists (fun n -> n = $"/{cp.Name}"))

    let! id =
      match container with
      | Some c -> c.ID |> Task.FromResult
      | None ->
        task {
          let! response = client.Containers.CreateContainerAsync(cp)
          return response.ID
        }

    do! client.Containers.StartContainerAsync(id, ContainerStartParameters()) :> Task

    return id
  }

let private eventStoreCreateParams port =
  let name = $"comm-mgmt-test-event-store-db-deleteme%i{port}"

  let hostConfig =
    let hostConfig = HostConfig()

    let http =
      PortBinding()
      |> fun b ->
           b.HostPort <- $"%i{port}/tcp"
           b.HostIP <- "0.0.0.0"
           b

    hostConfig.PortBindings <- Dictionary<string, IList<PortBinding>>()
    hostConfig.PortBindings.Add("2113/tcp", List<PortBinding>([ http ]))
    hostConfig

  let createParams = CreateContainerParameters()
  createParams.Name <- name
  createParams.Image <- "eventstore/eventstore:latest"
  createParams.Env <- List<string>()
  createParams.Env.Add("EVENTSTORE_CLUSTER_SIZE=1")
  createParams.Env.Add("EVENTSTORE_RUN_PROJECTIONS=All")
  createParams.Env.Add("EVENTSTORE_START_STANDARD_PROJECTIONS=true")
  createParams.Env.Add("EVENTSTORE_EXT_TCP_PORT=1113")
  createParams.Env.Add("EVENTSTORE_HTTP_PORT=2113")
  createParams.Env.Add("EVENTSTORE_INSECURE=true")
  createParams.Env.Add("EVENTSTORE_ENABLE_EXTERNAL_TCP=true")
  createParams.Env.Add("EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=true")
  createParams.HostConfig <- hostConfig

  createParams

let sm = new SemaphoreSlim(1)

let getConnectionInfo () =
  IPGlobalProperties
    .GetIPGlobalProperties()
    .GetActiveTcpConnections()

let random = Random()
let getRandomPort () = 30_000 |> random.Next |> (+) 10_000

let getFreePort () =
  let rec go pn =
    let isTaken =
      getConnectionInfo ()
      |> Seq.exists (fun ci -> ci.LocalEndPoint.Port = pn)

    match isTaken with
    | true -> getRandomPort () |> go
    | false -> pn

  sm.Wait()
  let port = getRandomPort () |> go
  sm.Release() |> ignore
  port

let testSetup () =
  task {
    let containerPort = getFreePort ()

    let! containerID =
      startContainer
      <| eventStoreCreateParams containerPort

    let driver =
      let driverOptions =
        let dO = FirefoxOptions()
        dO.AddArgument "--headless"
        dO

      new FirefoxDriver(driverOptions)

    let mutable ln: SendNotificationParams option = None

    let getLastNotification () =
      match ln with
      | Some n -> n
      | None -> failwith "Expected a notification"

    let sitePort = getFreePort ()
    let baseUrl = $"http://localhost:{sitePort}"

    let config =
      { EventStoreConnectionString = $"esdb://admin:changeit@localhost:{containerPort}?tls=false"
        BaseUrl = baseUrl
        AdminEmail = "notareal@email.com"
        SendGridKey = ""
        MailFrom = "" }

    let ports: IPorts =
      let mainPorts = Main.ports config

      { new IPorts with
          member this.sendEvent p = mainPorts.sendEvent p

          member this.sendNotification t n =
            ln <- Some n
            TaskResult.ok ()

          member this.configuration = mainPorts.configuration
          member this.save a = mainPorts.save a
          member this.find id = mainPorts.find id
          member this.query predicate = mainPorts.query predicate
          member this.delete<'a> id = mainPorts.delete<'a> id
          member this.getAll<'a>() = mainPorts.getAll<'a> () }

    let! host =
      task {
        let h = Main.buildHost ports <| Some sitePort
        do! h.StartAsync()
        return h
      }

    let disposers =
      [ fun () -> deleteContainer containerID |> fun t -> t.Result
        host.Dispose
        driver.Dispose ]

    driver.Url <- baseUrl

    return new Setup(disposers, getLastNotification, driver, ports)
  }
