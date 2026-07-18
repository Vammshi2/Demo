using System.ComponentModel.DataAnnotations;

namespace HostelPro.Models;

public sealed class DashboardStats
{
    public int TotalRooms { get; set; }
    public int TotalBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public int AvailableBeds { get; set; }
    public int ActiveTenants { get; set; }
    public int PendingBills { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal PaymentsToday { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal NetProfit => MonthlyRevenue - MonthlyExpenses;
    public int NewEnquiries { get; set; }
    public int OverdueBills { get; set; }
    public int OpenMaintenanceTickets { get; set; }
    public int PendingKycDocuments { get; set; }
    public double OccupancyPercentage => TotalBeds == 0 ? 0 : Math.Round(OccupiedBeds * 100d / TotalBeds, 1);
}

public sealed class RoomForm
{
    public Guid? Id { get; set; }

    [Required, MaxLength(30)]
    public string RoomNumber { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string FloorNumber { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string RoomType { get; set; } = "single";

    [Range(1, 20)]
    public int TotalBeds { get; set; } = 1;

    [Range(0, 100000)]
    public decimal MonthlyRent { get; set; }

    [Required]
    public string Status { get; set; } = "active";

    [MaxLength(1200)]
    public string Amenities { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(900)]
    public string CoverImageUrl { get; set; } = string.Empty;
}

public sealed class BedForm
{
    [Required]
    public Guid RoomId { get; set; }

    [Required, MaxLength(20)]
    public string BedNumber { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = "available";
}

public sealed class TenantForm
{
    public Guid? Id { get; set; }

    [Required, MaxLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Gender { get; set; } = "not_specified";

    [Required]
    public Guid RoomId { get; set; }

    public Guid? BedId { get; set; }

    [Range(0, 100000)]
    public decimal MonthlyRent { get; set; }

    [Range(1, 1000000)]
    public decimal SecurityDepositAmount { get; set; }

    [Range(1, 365)]
    public int NoticePeriodDays { get; set; } = 30;

    public DateOnly JoiningDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(120)]
    public string EmergencyContact { get; set; } = string.Empty;

    [MaxLength(30)]
    public string KycStatus { get; set; } = "pending";

    [Required]
    public string Status { get; set; } = "active";
}

public sealed class TenantApplicationForm
{
    [Required, MaxLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Gender { get; set; } = "not_specified";

    [Required, MaxLength(40)]
    public string PreferredRoomType { get; set; } = "any";

    public Guid? PreferredRoomId { get; set; }

    public Guid? PreferredBedId { get; set; }

    public decimal AdvanceAmount { get; set; }

    [MaxLength(120)]
    public string Occupation { get; set; } = string.Empty;

    [MaxLength(120)]
    public string EmergencyContact { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Website { get; set; } = string.Empty;
}

public sealed class TenantApplicationApprovalForm
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    public Guid RoomId { get; set; }

    public Guid? BedId { get; set; }

    [Range(0, 1000000)]
    public decimal MonthlyRent { get; set; }

    [Range(1, 1000000)]
    public decimal SecurityDepositAmount { get; set; }

    [Range(1, 365)]
    public int NoticePeriodDays { get; set; } = 30;

    public DateOnly JoiningDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
}

public sealed class TenantNoticeForm
{
    [Required]
    public Guid TenantId { get; set; }

    public DateOnly PlannedVacateDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
}

public sealed class TenantVacateForm
{
    [Required]
    public Guid TenantId { get; set; }

    [Range(0, 1000000)]
    public decimal Deductions { get; set; }

    [MaxLength(600)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class BillForm
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public string BillMonth { get; set; } = DateTime.Today.ToString("MMMM");

    [Range(2020, 2100)]
    public int BillYear { get; set; } = DateTime.Today.Year;

    [Range(0, 100000)]
    public decimal RentAmount { get; set; }

    [Range(0, 100000)]
    public decimal ElectricityCharges { get; set; }

    [Range(0, 100000)]
    public decimal MaintenanceCharges { get; set; }

    [Range(0, 100000)]
    public decimal OtherCharges { get; set; }

    [Range(0, 100000)]
    public decimal Discount { get; set; }

    [Range(0, 100000)]
    public decimal LateFeeAmount { get; set; }

    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(7));
}

public sealed class PaymentForm
{
    [Required]
    public Guid BillId { get; set; }

    [Range(1, 1000000)]
    public decimal PaymentAmount { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = "upi";

    [MaxLength(120)]
    public string TransactionId { get; set; } = string.Empty;
}

public sealed class PublicPaymentAttemptForm
{
    [Required, MaxLength(120)]
    public string TransactionId { get; set; } = string.Empty;
}

public sealed record CreatedPaymentLink(Guid Id, string Token, DateTime ExpiresUtc);

public sealed class PaymentWebhookRequest
{
    [Required, MaxLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Range(1, 1000000)]
    public decimal Amount { get; set; }

    [Required, MaxLength(120)]
    public string TransactionId { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Provider { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Status { get; set; } = string.Empty;
}

public sealed class ContactForm
{
    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(1200)]
    public string Message { get; set; } = string.Empty;
}

public sealed class UserAccountForm
{
    public string? Id { get; set; }

    [Required, MaxLength(160)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    [Required, MaxLength(30)]
    public string Role { get; set; } = AppRoles.User;

    [MinLength(10), MaxLength(100)]
    public string TemporaryPassword { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class GalleryForm
{
    public Guid? Id { get; set; }

    [Required, MaxLength(80)]
    public string Album { get; set; } = "Rooms";

    [MaxLength(900)]
    public string ImageUrl { get; set; } = string.Empty;

    [Required, MaxLength(220)]
    public string Caption { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class AmenityForm
{
    public Guid? Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string IconKey { get; set; } = "shield";

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed class TestimonialForm
{
    public Guid? Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Role { get; set; } = string.Empty;

    [Required, MaxLength(700)]
    public string Quote { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed class ExpenseForm
{
    public Guid? Id { get; set; }

    [Required, MaxLength(80)]
    public string Category { get; set; } = "maintenance";

    [Required, MaxLength(220)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 10000000)]
    public decimal Amount { get; set; }

    public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(1200)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class TenantDocumentForm
{
    public Guid? Id { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required, MaxLength(80)]
    public string DocumentType { get; set; } = "Aadhaar";

    [MaxLength(900)]
    public string FileUrl { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Status { get; set; } = "pending";

    [MaxLength(600)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class MaintenanceTicketForm
{
    public Guid? Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? RoomId { get; set; }

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1200)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Priority { get; set; } = "medium";

    [Required, MaxLength(30)]
    public string Status { get; set; } = "open";

    [MaxLength(120)]
    public string AssignedTo { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string ResolutionNotes { get; set; } = string.Empty;
}

public sealed class MessMenuForm
{
    public Guid? Id { get; set; }
    public DateOnly MenuDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, MaxLength(30)]
    public string MealType { get; set; } = "dinner";

    [Required, MaxLength(1200)]
    public string MenuItems { get; set; } = string.Empty;

    [Range(0, 10000)]
    public int OptInCount { get; set; }

    [Range(0, 10000)]
    public int OptOutCount { get; set; }
}

public sealed class MessAttendanceForm
{
    [Required]
    public Guid TenantId { get; set; }

    public DateOnly MealDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, MaxLength(30)]
    public string MealType { get; set; } = "dinner";

    [Required, MaxLength(30)]
    public string Status { get; set; } = "opt_in";
}
