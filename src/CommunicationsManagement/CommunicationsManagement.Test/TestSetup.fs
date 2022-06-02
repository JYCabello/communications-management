module TestProject1CommunicationsManagement.Test.TestSetup

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading.Tasks
open Docker.DotNet
open Docker.DotNet.Models

let getDockerClient () =
  let defaultWindowsDockerEngineUri = Uri("npipe://./pipe/docker_engine");
  let defaultLinuxDockerEngineUri = Uri("unix:///var/run/docker.sock")
  let engineUri =
    if RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
    then defaultLinuxDockerEngineUri
    else defaultWindowsDockerEngineUri
  (new DockerClientConfiguration (engineUri)).CreateClient()

let private deleteContainer id =
  task {
    use client = getDockerClient()
    do! client.Containers.StopContainerAsync(id, ContainerStopParameters()) :> Task
    do! client.Containers.RemoveContainerAsync(id, ContainerRemoveParameters())
  }

type Setup(disposers: (unit -> unit) list) =
  interface IDisposable with
    member this.Dispose() =
      for disposer in disposers do disposer ()

let private startContainer (cp: CreateContainerParameters) =
  task {
    use client = getDockerClient()
    let! containers =
      client
        .Containers
        .ListContainersAsync(
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

let private eventStoreCreateParams =
  let name = "comm-mgmt-test-event-store-db-deleteme"
  let hostConfig =
    let hostConfig = HostConfig()
    let http =
      PortBinding()
      |> fun b ->
        b.HostPort <- "2113/tcp"
        b.HostIP <- "0.0.0.0"
        b
    let other =
      PortBinding()
      |> fun b ->
        b.HostPort <- "1113/tcp"
        b.HostIP <- "0.0.0.0"
        b

    hostConfig.PortBindings <-
      Dictionary<string, IList<PortBinding>>()
    hostConfig.PortBindings.Add("2113/tcp",List<PortBinding>([http]))
    hostConfig.PortBindings.Add("1113/tcp",List<PortBinding>([other]))
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

let testSetup () =
  task {
    let! containerID = startContainer eventStoreCreateParams
    
    let disposers = 
      [ fun () -> deleteContainer containerID |> fun t -> t.Result ]
    return new Setup(disposers)
  }
