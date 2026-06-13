using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Data;
using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Web.Services;

public static class AuthenticationSeeder
{
    private static readonly Guid DefaultRenoCompanyID = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly IReadOnlyList<string> RoleNames = ["User", "Admin", "SuperAdmin"];
    private static readonly IReadOnlyList<(string Name, string Value)> DefaultAppSettings =
    [
        ("NewUserTokenExpiratinHours", "72")
    ];
    private static readonly IReadOnlyList<string> BuildingTypeNames = ["Primary Structure", "Detached Garage", "Outbuilding"];
    private static readonly IReadOnlyList<(Guid Id, string Name, int SortOrder)> InspectionAreaCategories =
    [
        (Guid.Parse("11111111-1111-1111-1111-000000000001"), "Exterior", 10),
        (Guid.Parse("11111111-1111-1111-1111-000000000002"), "Interior", 20),
        (Guid.Parse("11111111-1111-1111-1111-000000000003"), "Systems", 30)
    ];
    private static readonly IReadOnlyList<(string CategoryName, string Name, int SortOrder)> InspectionAreaTypes =
    [
        ("Exterior", "Roof", 10),
        ("Exterior", "Siding", 20),
        ("Exterior", "Foundation", 30),
        ("Exterior", "Windows and Doors", 40),
        ("Interior", "Kitchen", 10),
        ("Interior", "Bathroom", 20),
        ("Interior", "Bedroom", 30),
        ("Interior", "Living Area", 40),
        ("Systems", "Electrical", 10),
        ("Systems", "Plumbing", 20),
        ("Systems", "HVAC", 30)
    ];

    public static async Task SeedAuthenticationAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RenovatorAppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();

        var company = await EnsureDefaultCompanyAsync(dbContext, cancellationToken);
        var roles = await EnsureRolesAsync(dbContext, cancellationToken);
        await EnsureDefaultUserAsync(dbContext, passwordService, company, roles, cancellationToken);
        await EnsureDefaultAppSettingsAsync(dbContext, company.RenoCompanyID, cancellationToken);
        await EnsureLookupDataAsync(dbContext, company.RenoCompanyID, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<RenoCompany> EnsureDefaultCompanyAsync(RenovatorAppDbContext dbContext, CancellationToken cancellationToken)
    {
        var company = await dbContext.RenoCompanies.FirstOrDefaultAsync(item => item.RenoCompanyID == DefaultRenoCompanyID, cancellationToken);

        if (company is null)
        {
            company = new RenoCompany
            {
                RenoCompanyID = DefaultRenoCompanyID,
                DateCreated = DateTime.UtcNow
            };
            dbContext.RenoCompanies.Add(company);
        }

        company.Name = "RenovatorApp";
        company.Active = true;
        return company;
    }

    private static async Task<Dictionary<string, Role>> EnsureRolesAsync(RenovatorAppDbContext dbContext, CancellationToken cancellationToken)
    {
        var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in RoleNames)
        {
            var role = await dbContext.Roles.FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);
            if (role is null)
            {
                role = new Role { Name = roleName };
                dbContext.Roles.Add(role);
            }

            roles[roleName] = role;
        }

        return roles;
    }

    private static async Task EnsureDefaultUserAsync(
        RenovatorAppDbContext dbContext,
        PasswordService passwordService,
        RenoCompany company,
        IReadOnlyDictionary<string, Role> roles,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.RenoUsers
            .Include(item => item.UserRoles)
            .FirstOrDefaultAsync(item => item.Login == "Courtney", cancellationToken);

        if (user is null)
        {
            user = new RenoUser
            {
                RenoCompanyID = company.RenoCompanyID,
                Login = "Courtney",
                Password = passwordService.HashPassword("Password1!"),
                FirstName = "Courtney",
                LastName = string.Empty,
                Active = true,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow
            };
            dbContext.RenoUsers.Add(user);
        }
        else
        {
            user.RenoCompanyID = company.RenoCompanyID;
            user.Active = true;
            user.DateModified = DateTime.UtcNow;
        }

        EnsureUserRole(dbContext, user, roles["Admin"]);
        EnsureUserRole(dbContext, user, roles["SuperAdmin"]);
    }

    private static void EnsureUserRole(RenovatorAppDbContext dbContext, RenoUser user, Role role)
    {
        if (user.UserRoles.Any(item => item.Role == role || item.RoleID == role.RoleID))
        {
            return;
        }

        dbContext.UserRoles.Add(new UserRole { User = user, Role = role });
    }

    private static async Task EnsureDefaultAppSettingsAsync(
        RenovatorAppDbContext dbContext,
        Guid renoCompanyID,
        CancellationToken cancellationToken)
    {
        foreach (var settingSeed in DefaultAppSettings)
        {
            var setting = await dbContext.AppSettings.FirstOrDefaultAsync(
                item => item.RenoCompanyID == renoCompanyID && item.Name == settingSeed.Name,
                cancellationToken);

            if (setting is null)
            {
                dbContext.AppSettings.Add(new AppSetting
                {
                    RenoCompanyID = renoCompanyID,
                    Name = settingSeed.Name,
                    Value = settingSeed.Value
                });
            }
        }
    }

    private static async Task EnsureLookupDataAsync(RenovatorAppDbContext dbContext, Guid renoCompanyID, CancellationToken cancellationToken)
    {
        foreach (var buildingTypeName in BuildingTypeNames)
        {
            var exists = await dbContext.BuildingTypes.AnyAsync(
                item => item.RenoCompanyID == renoCompanyID && item.Name == buildingTypeName,
                cancellationToken);

            if (!exists)
            {
                dbContext.BuildingTypes.Add(new BuildingType { RenoCompanyID = renoCompanyID, Name = buildingTypeName });
            }
        }

        foreach (var categorySeed in InspectionAreaCategories)
        {
            var category = await dbContext.InspectionAreaCategories.FirstOrDefaultAsync(
                item => item.RenoCompanyID == renoCompanyID && item.Name == categorySeed.Name,
                cancellationToken);

            if (category is null)
            {
                category = new InspectionAreaCategory
                {
                    InspectionAreaCategoryId = categorySeed.Id,
                    RenoCompanyID = renoCompanyID,
                    Name = categorySeed.Name
                };
                dbContext.InspectionAreaCategories.Add(category);
            }

            category.SortOrder = categorySeed.SortOrder;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var categories = await dbContext.InspectionAreaCategories
            .Where(item => item.RenoCompanyID == renoCompanyID)
            .ToDictionaryAsync(item => item.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var areaTypeSeed in InspectionAreaTypes)
        {
            if (!categories.TryGetValue(areaTypeSeed.CategoryName, out var category))
            {
                continue;
            }

            var areaType = await dbContext.InspectionAreaTypes.FirstOrDefaultAsync(
                item => item.RenoCompanyID == renoCompanyID && item.Name == areaTypeSeed.Name,
                cancellationToken);

            if (areaType is null)
            {
                areaType = new InspectionAreaType
                {
                    RenoCompanyID = renoCompanyID,
                    Name = areaTypeSeed.Name
                };
                dbContext.InspectionAreaTypes.Add(areaType);
            }

            areaType.CategoryId = category.InspectionAreaCategoryId;
            areaType.SortOrder = areaTypeSeed.SortOrder;
        }
    }
}
