using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(120).IsRequired();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

            e.HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId);
        });

        modelBuilder.Entity<Shift>(e =>
        {
            e.Property(s => s.Position).HasMaxLength(60).IsRequired();
            e.Property(s => s.Notes).HasMaxLength(500);
            e.HasIndex(s => new { s.CompanyId, s.StartsAt });

            e.HasOne<Company>()
                .WithMany()
                .HasForeignKey(s => s.CompanyId);

            // Restrict here, otherwise SQL Server complains about multiple cascade paths
            // (Company -> User -> Shift and Company -> Shift).
            e.HasOne(s => s.Employee)
                .WithMany()
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SwapRequest>(e =>
        {
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Note).HasMaxLength(500);

            e.HasOne<Company>()
                .WithMany()
                .HasForeignKey(r => r.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            // Deleting a shift removes its swap history too.
            e.HasOne(r => r.Shift)
                .WithMany()
                .HasForeignKey(r => r.ShiftId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.RequestedBy)
                .WithMany()
                .HasForeignKey(r => r.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(r => r.TargetEmployee)
                .WithMany()
                .HasForeignKey(r => r.TargetEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
