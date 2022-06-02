﻿module TestProject1CommunicationsManagement.Test.TestSetup

open System
open System.Collections.Generic
open System.Net.NetworkInformation
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open CommunicationsManagement.API.Models
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
    sitePort
  ) =

  member this.lastNotification = getLastNotification ()

  member this.baseUrl = $"http://localhost:{sitePort}"

  member this.driver = webDriver

  interface IDisposable with
    member this.Dispose() =
      for disposer in disposers do
        disposer ()

let getDockerClient () =
  let defaultWindowsDockerEngineUri = Uri("npipe://./pipe/docker_engine")
  let defaultLinuxDockerEngineUri = Uri("unix:///var/run/docker.sock")

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
  task {
    let rec go pn =
      let isTaken =
        getConnectionInfo ()
        |> Seq.exists (fun ci -> ci.LocalEndPoint.Port = pn)

      match isTaken with
      | true -> getRandomPort () |> go
      | false -> pn

    do! sm.WaitAsync()
    let port = getRandomPort () |> go
    sm.Release() |> ignore
    return port
  }

let testSetup () =
  task {
    let! containerPort = getFreePort ()
    let! containerID = startContainer <| eventStoreCreateParams containerPort

    let driver =
      let driverOptions =
        let dO = FirefoxOptions()
        dO.AddArgument "--headless"
        dO

      new FirefoxDriver(driverOptions)

    let mutable ln: SendNotificationParams option = None

    let ports: IPorts =
      { new IPorts with
          member this.sendEvent p = () |> TaskResult.ok
          member this.sendNotification p = taskResult { ln <- Some p }

          member this.configuration =
            { EventStoreConnectionString =
                $"esdb://admin:changeit@localhost:{containerPort}?tls=false" } }

    let getLastNotification () =
      match ln with
      | Some n -> n
      | None -> failwith "Expected a notification"

    let! sitePort = getFreePort ()

    
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

    return new Setup(disposers, getLastNotification, driver, sitePort)
  }