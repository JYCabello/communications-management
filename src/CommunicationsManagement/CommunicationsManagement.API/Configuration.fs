module CommunicationsManagement.API.Configuration

open System.IO
open Models
open Microsoft.Extensions.Configuration

let configuration =
  ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .AddEnvironmentVariables()
    .Build()
    .Get<Configuration>()
