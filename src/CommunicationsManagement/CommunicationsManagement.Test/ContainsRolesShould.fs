module TestProject1CommunicationsManagement.Test.ContainsRolesShould

open CommunicationsManagement.API
open Xunit
open Models

[<Theory>]
[<InlineData(true, Roles.Delegate, Roles.Admin)>]
[<InlineData(true, Roles.Press, Roles.Admin)>]
[<InlineData(true, Roles.Admin, Roles.Admin)>]
[<InlineData(true, Roles.Press, Roles.Press)>]
[<InlineData(true, Roles.Delegate, Roles.Delegate)>]
[<InlineData(false, Roles.Press, Roles.Delegate)>]
[<InlineData(false, Roles.Admin, Roles.Delegate)>]
[<InlineData(false, Roles.Delegate, Roles.Press)>]
[<InlineData(false, Roles.Admin, Roles.Press)>]
let ``process roles correctly`` result searchTerm userRoles =
  Assert.Equal(result, contains searchTerm userRoles)
