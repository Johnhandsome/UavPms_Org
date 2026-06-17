using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UavPms.Core.Entities;

namespace UavPms.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1. Seed Roles
        var defaultRoles = new List<string> { "SystemAdmin", "Manager", "Inspector", "Analyst", "Technician" };
        var existingRoles = await context.Roles.ToListAsync();
        var rolesToCreate = defaultRoles.Where(r => !existingRoles.Any(er => er.RoleName == r)).ToList();

        foreach (var roleName in rolesToCreate)
        {
            context.Roles.Add(new Role
            {
                RoleName = roleName,
                Description = $"Default role for {roleName}"
            });
        }

        if (rolesToCreate.Any())
        {
            await context.SaveChangesAsync();
        }

        // 2. Seed default Admin User
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "SystemAdmin");
        if (adminRole != null)
        {
            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            if (adminUser == null)
            {
                var adminPassword = Environment.GetEnvironmentVariable("UAVPMS_ADMIN_PASSWORD")
                     ?? throw new InvalidOperationException("Missing UAVPMS_ADMIN_PASSWORD environment variable for seeding default admin user.");
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword, 10);

                var newAdmin = new User
                {
                    Username = "admin",
                    PasswordHash = passwordHash,
                    FullName = "System Administrator",
                    Email = "admin@uavpms.com",
                    Phone = "0123456789",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(newAdmin);
                await context.SaveChangesAsync();

                // Assign role
                context.UserRoles.Add(new UserRole
                {
                    UserId = newAdmin.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
