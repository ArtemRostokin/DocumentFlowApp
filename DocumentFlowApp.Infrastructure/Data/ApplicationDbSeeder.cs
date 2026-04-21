using DocumentFlowApp.Core.Entities;
using DocumentFlowApp.Core.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentFlowApp.Infrastructure.Data;

public static class ApplicationDbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.MigrateAsync(cancellationToken);

        var adminRole = await EnsureRoleAsync(context, AppRoles.Admin, "Администратор системы", cancellationToken);
        var managerRole = await EnsureRoleAsync(context, AppRoles.Manager, "Менеджер документов", cancellationToken);
        var employeeRole = await EnsureRoleAsync(context, AppRoles.Employee, "Исполнитель", cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await EnsureUserAsync(
            context,
            adminRole.RoleId,
            "admin",
            "admin@docflow.local",
            "Admin123!",
            "Системный",
            "Администратор",
            cancellationToken);

        await EnsureUserAsync(
            context,
            managerRole.RoleId,
            "manager",
            "manager@docflow.local",
            "Manager123!",
            "Ирина",
            "Менеджер",
            cancellationToken);

        await EnsureUserAsync(
            context,
            employeeRole.RoleId,
            "employee",
            "employee@docflow.local",
            "Employee123!",
            "Алексей",
            "Исполнитель",
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Role> EnsureRoleAsync(
        ApplicationDbContext context,
        string roleName,
        string description,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == roleName, cancellationToken);
        if (role is not null)
            return role;

        role = new Role
        {
            RoleName = roleName,
            Description = description
        };

        context.Roles.Add(role);
        return role;
    }

    private static async Task EnsureUserAsync(
        ApplicationDbContext context,
        int roleId,
        string userName,
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existingUser is not null)
            return;

        var user = new User
        {
            RoleId = roleId,
            UserName = userName,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true
        };

        var passwordHasher = new PasswordHasher<User>();
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        context.Users.Add(user);
    }
}
