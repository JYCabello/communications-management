module CommunicationsManagement.API.Routes

open System.Threading.Tasks
open CommunicationsManagement.API.Effects
open Microsoft.AspNetCore.Http
open Giraffe.Core

let login (ports: IPorts) (next: HttpFunc) (ctx: HttpContext): Task<HttpContext option> =
  failwith "todo"
