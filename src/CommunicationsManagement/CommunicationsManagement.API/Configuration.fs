module CommunicationsManagement.API.Configuration

open System.IO
open Models
open Microsoft.Extensions.Configuration

let configuration =
  ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", false, false)
    .AddJsonFile("appsettings.development.json", true, false)
    .AddEnvironmentVariables()
    .Build()
    .Get<Configuration>()
