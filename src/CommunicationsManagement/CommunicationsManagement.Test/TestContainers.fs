﻿module TestProject1CommunicationsManagement.Test.TestContainers

open System
open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading.Tasks
open Docker.DotNet
open Docker.DotNet.Models

let startEventStore name =
  task {
    let defaultWindowsDockerEngineUri = Uri("npipe://./pipe/docker_engine");
    let defaultLinuxDockerEngineUri = Uri("unix:///var/run/docker.sock")
    let engineUri =
      if RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
      then defaultLinuxDockerEngineUri
      else defaultWindowsDockerEngineUri
    use client = (new DockerClientConfiguration (engineUri)).CreateClient()
    let parameters = CreateContainerParameters()
    parameters.Name <- name
    parameters.Image <- "eventstore/eventstore:latest"
    parameters.ExposedPorts <- Dictionary<string, EmptyStruct>()
    parameters.ExposedPorts.Add("2113/tcp", EmptyStruct())
    parameters.ExposedPorts.Add("1113/tcp", EmptyStruct())
    parameters.Env <- List<string>()
    parameters.Env.Add("EVENTSTORE_CLUSTER_SIZE=1")
    parameters.Env.Add("EVENTSTORE_RUN_PROJECTIONS=All")
    parameters.Env.Add("EVENTSTORE_START_STANDARD_PROJECTIONS=true")
    parameters.Env.Add("EVENTSTORE_EXT_TCP_PORT=1113")
    parameters.Env.Add("EVENTSTORE_HTTP_PORT=2113")
    parameters.Env.Add("EVENTSTORE_INSECURE=true")
    parameters.Env.Add("EVENTSTORE_ENABLE_EXTERNAL_TCP=true")
    parameters.Env.Add("EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP=true")
    
    let hostConfig = HostConfig()
    let bindings =
      PortBinding()
      |> fun b ->
        b.HostPort <- "2113"
        b.HostIP <- "0.0.0.0"
        List<PortBinding>([b])

    hostConfig.PortBindings <-
      Dictionary<string, IList<PortBinding>>()
    hostConfig.PortBindings.Add("2113", bindings)
  
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
      |> Seq.tryFind (fun c -> c.Names |> Seq.exists (fun n -> n = $"/{name}"))

    let! id =
      match container with
      | Some c -> c.ID |> Task.FromResult 
      | None ->
        task {
          let! response = client.Containers.CreateContainerAsync(parameters)
          return response.ID
        }
    do! client.Containers.StartContainerAsync(id, ContainerStartParameters()) :> Task
  }
