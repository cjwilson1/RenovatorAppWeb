using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Web.Services;

public static class AuthenticationSeeder
{
    public static async Task SeedAuthenticationAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RenovatorAppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in new[] { "User", "Admin", "SuperAdmin" })
        {
            var role = await dbContext.Roles.FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);
            if (role is null)
            {
                role = new Role { Name = roleName };
                dbContext.Roles.Add(role);
            }

            roles[roleName] = role;
        }

        if (await dbContext.RenoUsers.AnyAsync(cancellationToken))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var company = await dbContext.RenoCompanies.FirstOrDefaultAsync(cancellationToken)
            ?? new RenoCompany
            {
                Name = configuration["InitialAdmin:CompanyName"] ?? "RenovatorApp",
                Active = true
            };

        if (dbContext.Entry(company).State == EntityState.Detached)
        {
            dbContext.RenoCompanies.Add(company);
        }

        var password = configuration["InitialAdmin:Password"] ?? configuration["INITIAL_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var user = new RenoUser
        {
            RenoCompanyID = company.RenoCompanyID,
            Login = configuration["InitialAdmin:Login"] ?? configuration["INITIAL_ADMIN_LOGIN"] ?? "admin",
            Password = passwordService.HashPassword(password),
            FirstName = "Super",
            LastName = "Admin",
            Email = configuration["InitialAdmin:Email"] ?? string.Empty,
            Active = true
        };

        dbContext.RenoUsers.Add(user);
        dbContext.UserRoles.Add(new UserRole { User = user, Role = roles["SuperAdmin"] });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
