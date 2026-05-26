using Microsoft.EntityFrameworkCore;
using RenovatorApp.Infrastructure.Models;

namespace RenovatorApp.Infrastructure.Data;

public sealed class RenovatorAppDbContext : DbContext
{
    public RenovatorAppDbContext(DbContextOptions<RenovatorAppDbContext> options) : base(options)
    {
    }

    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingType> BuildingTypes => Set<BuildingType>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionArea> InspectionAreas => Set<InspectionArea>();
    public DbSet<InspectionAreaCategory> InspectionAreaCategories => Set<InspectionAreaCategory>();
    public DbSet<InspectionAreaNote> InspectionAreaNotes => Set<InspectionAreaNote>();
    public DbSet<InspectionAreaNoteEstimateItem> InspectionAreaNoteEstimateItems => Set<InspectionAreaNoteEstimateItem>();
    public DbSet<InspectionAreaNotePhoto> InspectionAreaNotePhotos => Set<InspectionAreaNotePhoto>();
    public DbSet<InspectionAreaType> InspectionAreaTypes => Set<InspectionAreaType>();
    public DbSet<Inspector> Inspectors => Set<Inspector>();
    public DbSet<MileageTracking> MileageTracking => Set<MileageTracking>();
    public DbSet<MileageTrackingWaypoint> MileageTrackingWaypoints => Set<MileageTrackingWaypoint>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<PartSource> PartSources => Set<PartSource>();
    public DbSet<Property> Properties => Set<Property>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTables(modelBuilder);
        ConfigureKeys(modelBuilder);
        ConfigureRelationships(modelBuilder);
        ConfigureIndexes(modelBuilder);
        ConfigurePrecision(modelBuilder);
    }

    private static void ConfigureTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Address>().ToTable("Address");
        modelBuilder.Entity<AppSetting>().ToTable("Settings");
        modelBuilder.Entity<Building>().ToTable("Building");
        modelBuilder.Entity<BuildingType>().ToTable("BuildingType");
        modelBuilder.Entity<Customer>().ToTable("Customer");
        modelBuilder.Entity<Document>().ToTable("Documents");
        modelBuilder.Entity<Employee>().ToTable("Employee");
        modelBuilder.Entity<Inspection>().ToTable("Inspection");
        modelBuilder.Entity<InspectionArea>().ToTable("InspectionArea");
        modelBuilder.Entity<InspectionAreaCategory>().ToTable("InspectionAreaCategory");
        modelBuilder.Entity<InspectionAreaNote>().ToTable("InspectionAreaNote");
        modelBuilder.Entity<InspectionAreaNoteEstimateItem>().ToTable("InspectionAreaNoteEstimateItem");
        modelBuilder.Entity<InspectionAreaNotePhoto>().ToTable("InspectionAreaNotePhoto");
        modelBuilder.Entity<InspectionAreaType>().ToTable("InspectionAreaType");
        modelBuilder.Entity<Inspector>().ToTable("Inspectors");
        modelBuilder.Entity<MileageTracking>().ToTable("MileageTracking");
        modelBuilder.Entity<MileageTrackingWaypoint>().ToTable("MileageTrackingWaypoint");
        modelBuilder.Entity<Part>().ToTable("Part");
        modelBuilder.Entity<PartSource>().ToTable("PartSource");
        modelBuilder.Entity<Property>().ToTable("Property");
    }

    private static void ConfigureKeys(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Address>().HasKey(address => address.Id);
        modelBuilder.Entity<AppSetting>().HasKey(setting => setting.Id);
        modelBuilder.Entity<Building>().HasKey(building => building.Id);
        modelBuilder.Entity<BuildingType>().HasKey(buildingType => buildingType.Id);
        modelBuilder.Entity<Customer>().HasKey(customer => customer.CustomerId);
        modelBuilder.Entity<Document>().HasKey(document => document.DocumentId);
        modelBuilder.Entity<Employee>().HasKey(employee => employee.EmployeeId);
        modelBuilder.Entity<Inspection>().HasKey(inspection => inspection.Id);
        modelBuilder.Entity<InspectionArea>().HasKey(area => area.Id);
        modelBuilder.Entity<InspectionAreaCategory>().HasKey(category => category.Id);
        modelBuilder.Entity<InspectionAreaNote>().HasKey(note => note.Id);
        modelBuilder.Entity<InspectionAreaNoteEstimateItem>().HasKey(item => item.Id);
        modelBuilder.Entity<InspectionAreaNotePhoto>().HasKey(photo => photo.Id);
        modelBuilder.Entity<InspectionAreaType>().HasKey(areaType => areaType.AreaTypeId);
        modelBuilder.Entity<Inspector>().HasKey(inspector => inspector.Id);
        modelBuilder.Entity<MileageTracking>().HasKey(session => session.UniqueId);
        modelBuilder.Entity<MileageTrackingWaypoint>().HasKey(waypoint => waypoint.UniqueId);
        modelBuilder.Entity<Part>().HasKey(part => part.PartId);
        modelBuilder.Entity<PartSource>().HasKey(source => source.PartSourceId);
        modelBuilder.Entity<Property>().HasKey(property => property.Id);
    }

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Property>()
            .HasOne(property => property.Address)
            .WithOne(address => address.Property)
            .HasForeignKey<Address>(address => address.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Customer>()
            .HasOne(customer => customer.BillAddress)
            .WithMany(address => address.BillingCustomers)
            .HasForeignKey(customer => customer.BillAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Customer>()
            .HasOne(customer => customer.ShipAddress)
            .WithMany(address => address.ShippingCustomers)
            .HasForeignKey(customer => customer.ShipAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Employee>()
            .HasOne(employee => employee.PrimaryAddress)
            .WithMany(address => address.Employees)
            .HasForeignKey(employee => employee.PrimaryAddressId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Document>()
            .HasOne(document => document.Customer)
            .WithMany(customer => customer.Documents)
            .HasForeignKey(document => document.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Inspection>()
            .HasOne(inspection => inspection.Property)
            .WithMany(property => property.Inspections)
            .HasForeignKey(inspection => inspection.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Inspection>()
            .HasOne(inspection => inspection.Customer)
            .WithMany(customer => customer.Inspections)
            .HasForeignKey(inspection => inspection.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Building>()
            .HasOne(building => building.Property)
            .WithMany(property => property.Buildings)
            .HasForeignKey(building => building.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Building>()
            .HasOne(building => building.BuildingType)
            .WithMany(buildingType => buildingType.Buildings)
            .HasForeignKey(building => building.BuildingTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InspectionArea>()
            .HasOne(area => area.Property)
            .WithMany(property => property.Areas)
            .HasForeignKey(area => area.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InspectionArea>()
            .HasOne(area => area.Building)
            .WithMany(building => building.Areas)
            .HasForeignKey(area => area.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InspectionArea>()
            .HasOne(area => area.AreaType)
            .WithMany(areaType => areaType.Areas)
            .HasForeignKey(area => area.AreaTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InspectionAreaType>()
            .HasOne(areaType => areaType.Category)
            .WithMany(category => category.AreaTypes)
            .HasForeignKey(areaType => areaType.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InspectionAreaNote>()
            .HasOne(note => note.Area)
            .WithMany(area => area.AreaNotes)
            .HasForeignKey(note => note.AreaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InspectionAreaNoteEstimateItem>()
            .HasOne(item => item.AreaNote)
            .WithMany(note => note.EstimateItems)
            .HasForeignKey(item => item.AreaNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InspectionAreaNotePhoto>()
            .HasOne(photo => photo.AreaNote)
            .WithMany(note => note.Photos)
            .HasForeignKey(photo => photo.AreaNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Part>()
            .HasOne(part => part.PartSource)
            .WithMany(source => source.Parts)
            .HasForeignKey(part => part.PartSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MileageTrackingWaypoint>()
            .HasOne(waypoint => waypoint.MileageTracking)
            .WithMany(session => session.Waypoints)
            .HasForeignKey(waypoint => waypoint.MileageTrackingId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSetting>().HasIndex(setting => setting.Name).IsUnique();
        modelBuilder.Entity<BuildingType>().HasIndex(buildingType => buildingType.Name).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(customer => customer.QuickBooksCustomerId).IsUnique().HasFilter("\"QuickBooksCustomerId\" <> ''");
        modelBuilder.Entity<Customer>().HasIndex(customer => customer.DisplayName);
        modelBuilder.Entity<Customer>().HasIndex(customer => customer.CompanyName);
        modelBuilder.Entity<Customer>().HasIndex(customer => customer.FamilyName);
        modelBuilder.Entity<Document>().HasIndex(document => document.CustomerId);
        modelBuilder.Entity<Document>().HasIndex(document => document.CreateDate);
        modelBuilder.Entity<Document>().HasIndex(document => document.DocumentType);
        modelBuilder.Entity<Employee>().HasIndex(employee => employee.QuickBooksEmployeeId).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(employee => employee.DisplayName);
        modelBuilder.Entity<Employee>().HasIndex(employee => employee.FamilyName);
        modelBuilder.Entity<Inspection>().HasIndex(inspection => inspection.CreatedAtUtc);
        modelBuilder.Entity<Inspection>().HasIndex(inspection => inspection.UpdatedAtUtc);
        modelBuilder.Entity<Inspection>().HasIndex(inspection => inspection.InspectionDate);
        modelBuilder.Entity<InspectionAreaCategory>().HasIndex(category => category.Name).IsUnique();
        modelBuilder.Entity<InspectionAreaType>().HasIndex(areaType => areaType.Name).IsUnique();
        modelBuilder.Entity<MileageTracking>().HasIndex(session => session.TrackingStartedAtUtc);
        modelBuilder.Entity<MileageTrackingWaypoint>().HasIndex(waypoint => waypoint.MileageTrackingId);
        modelBuilder.Entity<MileageTrackingWaypoint>().HasIndex(waypoint => waypoint.WaypointTime);
        modelBuilder.Entity<Part>().HasIndex(part => part.Name);
        modelBuilder.Entity<PartSource>().HasIndex(source => source.Name).IsUnique();
    }

    private static void ConfigurePrecision(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Inspector>().Property(inspector => inspector.HourlyRate).HasPrecision(12, 2);
        modelBuilder.Entity<InspectionAreaNoteEstimateItem>().Property(item => item.Cost).HasPrecision(12, 2);
        modelBuilder.Entity<InspectionAreaNoteEstimateItem>().Property(item => item.Hours).HasPrecision(12, 2);
        modelBuilder.Entity<Part>().Property(part => part.Cost).HasPrecision(12, 2);
        modelBuilder.Entity<Customer>().Property(customer => customer.Balance).HasPrecision(12, 2);
        modelBuilder.Entity<Customer>().Property(customer => customer.BalanceWithJobs).HasPrecision(12, 2);
        modelBuilder.Entity<Employee>().Property(employee => employee.BillRate).HasPrecision(12, 2);
        modelBuilder.Entity<Employee>().Property(employee => employee.HourlyCostRate).HasPrecision(12, 2);
    }
}
