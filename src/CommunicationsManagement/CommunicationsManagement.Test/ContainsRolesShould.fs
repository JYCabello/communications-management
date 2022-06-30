module TestProject1CommunicationsManagement.Test.ContainsRolesShould

open CommunicationsManagement.API
open Xunit
open Models

[<Theory>]
// None has no roles, not even None
[<InlineData(false, Roles.Admin, Roles.None)>]
[<InlineData(false, Roles.Press, Roles.None)>]
[<InlineData(false, Roles.Delegate, Roles.None)>]
[<InlineData(false, Roles.UserManagement, Roles.None)>]
[<InlineData(false, Roles.ChannelManagement, Roles.None)>]
[<InlineData(false, Roles.None, Roles.None)>]
// Admin has all roles
[<InlineData(true, Roles.Delegate, Roles.Admin)>]
[<InlineData(true, Roles.Press, Roles.Admin)>]
[<InlineData(true, Roles.Admin, Roles.Admin)>]
[<InlineData(true, Roles.UserManagement, Roles.Admin)>]
[<InlineData(true, Roles.ChannelManagement, Roles.Admin)>]
[<InlineData(true, Roles.None, Roles.Admin)>]
// Roles match
[<InlineData(true, Roles.Press, Roles.Press)>]
[<InlineData(true, Roles.Press, Roles.Press ||| Roles.Delegate)>]
[<InlineData(true, Roles.Press, Roles.Press ||| Roles.UserManagement)>]
[<InlineData(true, Roles.Delegate, Roles.Delegate)>]
[<InlineData(true, Roles.Delegate, Roles.Delegate ||| Roles.UserManagement)>]
[<InlineData(true, Roles.Delegate, Roles.Delegate ||| Roles.Press)>]
[<InlineData(true, Roles.UserManagement, Roles.UserManagement)>]
[<InlineData(true, Roles.UserManagement, Roles.UserManagement ||| Roles.Press)>]
[<InlineData(true, Roles.UserManagement, Roles.UserManagement ||| Roles.Delegate)>]
[<InlineData(true, Roles.ChannelManagement, Roles.ChannelManagement)>]
// Roles not matching
[<InlineData(false, Roles.Press, Roles.Delegate ||| Roles.UserManagement)>]
[<InlineData(false, Roles.Delegate, Roles.Press ||| Roles.UserManagement)>]
[<InlineData(false, Roles.UserManagement, Roles.Press ||| Roles.Delegate)>]
let ``process roles correctly`` result searchTerm userRoles =
  Assert.Equal(result, contains searchTerm userRoles)
