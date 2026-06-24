using System;
using System.Collections.Generic;

namespace UavPms.Application.Features.Users.DTOs;

public class UserDetailDto 
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<RoleDto> Roles { get; set; } = new();
}