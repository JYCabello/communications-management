module CommunicationsManagement.API.Views.CreateUser

open CommunicationsManagement.API.Models

type UserCreationViewModel =
  { Name: string option
    NameError: string option
    Email: string option
    EmailError: string option
    Roles: Roles
    RolesError: string option }
