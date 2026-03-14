using Microsoft.EntityFrameworkCore;
using EquimonApi.Models;

namespace EquimonApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Department> Departments { get; set; }
    public DbSet<Machine> Machines { get; set; }
    public DbSet<Sensor> Sensors { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ParentId);
            
            // Self-referencing relationship for hierarchical structure
            entity.HasOne<Department>()
                  .WithMany(d => d.Children)
                  .HasForeignKey(d => d.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Manufacturer).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Brand).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DepartmentId).IsRequired();
            
            // Relationship to Department
            entity.HasOne(m => m.Department)
                  .WithMany()
                  .HasForeignKey(m => m.DepartmentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.Property(e => e.DataType).IsRequired();
            entity.Property(e => e.Threshold);
            entity.Property(e => e.MachineId).IsRequired();
            
            // Relationship to Machine
            entity.HasOne(s => s.Machine)
                  .WithMany()
                  .HasForeignKey(s => s.MachineId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
