using HostelPro.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HostelPro.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantApplication> TenantApplications => Set<TenantApplication>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentLink> PaymentLinks => Set<PaymentLink>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<TenantDocument> TenantDocuments => Set<TenantDocument>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<MessMenu> MessMenus => Set<MessMenu>();
    public DbSet<MessAttendance> MessAttendances => Set<MessAttendance>();
    public DbSet<Enquiry> Enquiries => Set<Enquiry>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<HostelSetting> HostelSettings => Set<HostelSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Room>()
            .HasIndex(room => room.RoomNumber)
            .IsUnique();

        builder.Entity<Amenity>()
            .HasIndex(amenity => amenity.Name)
            .IsUnique();

        builder.Entity<Bed>()
            .HasIndex(bed => new { bed.RoomId, bed.BedNumber })
            .IsUnique();

        builder.Entity<Tenant>()
            .HasOne(tenant => tenant.Bed)
            .WithMany()
            .HasForeignKey(tenant => tenant.BedId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Tenant>()
            .HasOne(tenant => tenant.Room)
            .WithMany()
            .HasForeignKey(tenant => tenant.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Tenant>()
            .HasIndex(tenant => tenant.Email);

        builder.Entity<TenantApplication>()
            .HasIndex(application => application.ApplicationNumber)
            .IsUnique();

        builder.Entity<TenantApplication>()
            .HasOne(application => application.Tenant)
            .WithMany()
            .HasForeignKey(application => application.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TenantApplication>()
            .HasOne(application => application.PreferredRoom)
            .WithMany()
            .HasForeignKey(application => application.PreferredRoomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TenantApplication>()
            .HasOne(application => application.PreferredBed)
            .WithMany()
            .HasForeignKey(application => application.PreferredBedId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ApplicationUser>()
            .HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(user => user.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ApplicationUser>()
            .HasIndex(user => user.TenantId)
            .IsUnique()
            .HasFilter("\"TenantId\" IS NOT NULL");

        builder.Entity<Payment>()
            .HasIndex(payment => payment.ReceiptNumber)
            .IsUnique();

        builder.Entity<PaymentLink>()
            .HasIndex(link => link.TokenHash)
            .IsUnique();

        builder.Entity<PaymentLink>()
            .HasOne(link => link.Bill)
            .WithMany()
            .HasForeignKey(link => link.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentLink>()
            .HasOne(link => link.Tenant)
            .WithMany()
            .HasForeignKey(link => link.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentAttempt>()
            .HasOne(attempt => attempt.PaymentLink)
            .WithMany(link => link.Attempts)
            .HasForeignKey(attempt => attempt.PaymentLinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PaymentAttempt>()
            .HasIndex(attempt => attempt.TransactionId)
            .IsUnique();

        builder.Entity<Payment>()
            .HasIndex(payment => payment.TransactionId)
            .IsUnique()
            .HasFilter("\"TransactionId\" <> ''");

        builder.Entity<Bill>()
            .HasIndex(bill => bill.InvoiceNumber)
            .IsUnique()
            .HasFilter("\"InvoiceNumber\" <> ''");

        builder.Entity<Bill>()
            .Ignore(bill => bill.TotalAmount);

        builder.Entity<TenantDocument>()
            .HasOne(document => document.Tenant)
            .WithMany()
            .HasForeignKey(document => document.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MaintenanceTicket>()
            .HasOne(ticket => ticket.Tenant)
            .WithMany()
            .HasForeignKey(ticket => ticket.TenantId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<MaintenanceTicket>()
            .HasOne(ticket => ticket.Room)
            .WithMany()
            .HasForeignKey(ticket => ticket.RoomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<MessMenu>()
            .HasIndex(menu => new { menu.MenuDate, menu.MealType })
            .IsUnique();

        builder.Entity<MessAttendance>()
            .HasOne(attendance => attendance.Tenant)
            .WithMany()
            .HasForeignKey(attendance => attendance.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MessAttendance>()
            .HasIndex(attendance => new { attendance.TenantId, attendance.MealDate, attendance.MealType })
            .IsUnique();

        builder.Entity<Room>()
            .Property(room => room.MonthlyRent)
            .HasPrecision(12, 2);

        builder.Entity<Tenant>()
            .Property(tenant => tenant.MonthlyRent)
            .HasPrecision(12, 2);

        builder.Entity<Bill>()
            .Property(bill => bill.RentAmount)
            .HasPrecision(12, 2);

        builder.Entity<Bill>()
            .Property(bill => bill.ElectricityCharges)
            .HasPrecision(12, 2);

        builder.Entity<Bill>()
            .Property(bill => bill.MaintenanceCharges)
            .HasPrecision(12, 2);

        builder.Entity<Bill>()
            .Property(bill => bill.OtherCharges)
            .HasPrecision(12, 2);

        builder.Entity<Bill>()
            .Property(bill => bill.Discount)
            .HasPrecision(12, 2);

        builder.Entity<Bill>()
            .Property(bill => bill.LateFeeAmount)
            .HasPrecision(12, 2);

        builder.Entity<Bill>()
            .Property(bill => bill.PaidAmount)
            .HasPrecision(12, 2);

        builder.Entity<Payment>()
            .Property(payment => payment.PaymentAmount)
            .HasPrecision(12, 2);

        builder.Entity<PaymentLink>()
            .Property(link => link.Amount)
            .HasPrecision(12, 2);

        builder.Entity<PaymentAttempt>()
            .Property(attempt => attempt.Amount)
            .HasPrecision(12, 2);

        builder.Entity<Expense>()
            .Property(expense => expense.Amount)
            .HasPrecision(12, 2);

        builder.Entity<HostelSetting>()
            .Property(setting => setting.LateFee)
            .HasPrecision(12, 2);
    }
}
