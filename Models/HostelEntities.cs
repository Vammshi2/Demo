using System.ComponentModel.DataAnnotations;

namespace HostelPro.Models;

public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(30)]
    public string RoomNumber { get; set; } = string.Empty;

    [MaxLength(30)]
    public string FloorNumber { get; set; } = string.Empty;

    [MaxLength(40)]
    public string RoomType { get; set; } = "single";

    public int TotalBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public decimal MonthlyRent { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "active";

    [MaxLength(1200)]
    public string Amenities { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(900)]
    public string CoverImageUrl { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public List<Bed> Beds { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
}

public sealed class Bed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }

    [MaxLength(20)]
    public string BedNumber { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "available";

    public Guid? TenantId { get; set; }

    public Room? Room { get; set; }
}

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Guid? BedId { get; set; }

    [MaxLength(160)]
    public string StudentName { get; set; } = string.Empty;

    [EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    public DateOnly CheckInDate { get; set; }
    public int DurationMonths { get; set; }
    public decimal Amount { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    [MaxLength(600)]
    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public Room? Room { get; set; }
    public Bed? Bed { get; set; }
}

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(160)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Gender { get; set; } = "not_specified";

    public Guid RoomId { get; set; }
    public Guid? BedId { get; set; }
    public decimal MonthlyRent { get; set; }
    public DateOnly JoiningDate { get; set; }
    public decimal SecurityDepositAmount { get; set; }
    public decimal SecurityDepositPaidAmount { get; set; }
    public decimal SecurityDepositDeductions { get; set; }
    public decimal SecurityDepositRefundedAmount { get; set; }
    public int NoticePeriodDays { get; set; } = 30;
    public DateOnly? NoticeGivenDate { get; set; }
    public DateOnly? PlannedVacateDate { get; set; }
    public DateOnly? VacatedDate { get; set; }

    [MaxLength(30)]
    public string SecurityDepositStatus { get; set; } = "pending";

    [MaxLength(600)]
    public string VacateNotes { get; set; } = string.Empty;

    [MaxLength(120)]
    public string EmergencyContact { get; set; } = string.Empty;

    [MaxLength(30)]
    public string KycStatus { get; set; } = "pending";

    [MaxLength(30)]
    public string Status { get; set; } = "active";

    public Room? Room { get; set; }
    public Bed? Bed { get; set; }
}

public sealed class TenantApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(24)]
    public string ApplicationNumber { get; set; } = string.Empty;

    [MaxLength(160)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Gender { get; set; } = "not_specified";

    [MaxLength(40)]
    public string PreferredRoomType { get; set; } = "any";

    public Guid? PreferredRoomId { get; set; }
    public Guid? PreferredBedId { get; set; }
    public decimal RoomPrice { get; set; }
    public decimal AdvanceAmount { get; set; }

    [MaxLength(120)]
    public string Occupation { get; set; } = string.Empty;

    [MaxLength(120)]
    public string EmergencyContact { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    public Guid? TenantId { get; set; }
    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedUtc { get; set; }

    [MaxLength(180)]
    public string ReviewedBy { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
    public Room? PreferredRoom { get; set; }
    public Bed? PreferredBed { get; set; }
}

public sealed class Bill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    [MaxLength(80)]
    public string BillMonth { get; set; } = string.Empty;

    public int BillYear { get; set; }
    public decimal RentAmount { get; set; }
    public decimal ElectricityCharges { get; set; }
    public decimal MaintenanceCharges { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal Discount { get; set; }
    public decimal LateFeeAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateOnly DueDate { get; set; }

    [MaxLength(40)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? InvoiceSentUtc { get; set; }

    public Tenant? Tenant { get; set; }

    public decimal TotalAmount => RentAmount + ElectricityCharges + MaintenanceCharges + OtherCharges + LateFeeAmount - Discount;
}

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillId { get; set; }
    public Guid TenantId { get; set; }

    [MaxLength(40)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public decimal PaymentAmount { get; set; }

    [MaxLength(40)]
    public string PaymentMethod { get; set; } = "upi";

    [MaxLength(40)]
    public string GatewayProvider { get; set; } = string.Empty;

    [MaxLength(120)]
    public string TransactionId { get; set; } = string.Empty;

    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "success";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Bill? Bill { get; set; }
    public Tenant? Tenant { get; set; }
}

public sealed class PaymentLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillId { get; set; }
    public Guid TenantId { get; set; }

    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [MaxLength(30)]
    public string Status { get; set; } = "active";

    public DateTime ExpiresUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidUtc { get; set; }

    [MaxLength(180)]
    public string CreatedBy { get; set; } = string.Empty;

    public Bill? Bill { get; set; }
    public Tenant? Tenant { get; set; }
    public List<PaymentAttempt> Attempts { get; set; } = [];
}

public sealed class PaymentAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentLinkId { get; set; }
    public decimal Amount { get; set; }

    [MaxLength(40)]
    public string PaymentMethod { get; set; } = "upi";

    [MaxLength(120)]
    public string TransactionId { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    public DateTime SubmittedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedUtc { get; set; }

    [MaxLength(180)]
    public string ReviewedBy { get; set; } = string.Empty;

    public PaymentLink? PaymentLink { get; set; }
}

public sealed class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(80)]
    public string Category { get; set; } = "maintenance";

    [MaxLength(220)]
    public string Title { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(1200)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TenantDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    [MaxLength(80)]
    public string DocumentType { get; set; } = "Aadhaar";

    [MaxLength(900)]
    public string FileUrl { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "pending";

    [MaxLength(600)]
    public string Notes { get; set; } = string.Empty;

    public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedUtc { get; set; }

    public Tenant? Tenant { get; set; }
}

public sealed class MaintenanceTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TenantId { get; set; }
    public Guid? RoomId { get; set; }

    [MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Priority { get; set; } = "medium";

    [MaxLength(30)]
    public string Status { get; set; } = "open";

    [MaxLength(120)]
    public string AssignedTo { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string ResolutionNotes { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }

    public Tenant? Tenant { get; set; }
    public Room? Room { get; set; }
}

public sealed class MessMenu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly MenuDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(30)]
    public string MealType { get; set; } = "dinner";

    [MaxLength(1200)]
    public string MenuItems { get; set; } = string.Empty;

    public int OptInCount { get; set; }
    public int OptOutCount { get; set; }
}

public sealed class MessAttendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateOnly MealDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [MaxLength(30)]
    public string MealType { get; set; } = "dinner";

    [MaxLength(30)]
    public string Status { get; set; } = "opt_in";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Tenant? Tenant { get; set; }
}

public sealed class Enquiry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "new";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GalleryImage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(80)]
    public string Album { get; set; } = "Rooms";

    [MaxLength(900)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(220)]
    public string Caption { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class Amenity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(400)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(40)]
    public string IconKey { get; set; } = "shield";

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed class Testimonial
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(700)]
    public string Quote { get; set; } = string.Empty;

    public int Rating { get; set; } = 5;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public sealed class HostelSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(160)]
    public string HostelName { get; set; } = "HostelPro";

    [MaxLength(30)]
    public string PropertyModel { get; set; } = "pg";

    [MaxLength(30)]
    public string ResidentCategory { get; set; } = "mixed";

    [MaxLength(120)]
    public string Tagline { get; set; } = "Premium PG & Hostel Accommodation";

    [MaxLength(160)]
    public string HeroTitle { get; set; } = "Your Home";

    [MaxLength(160)]
    public string HeroHighlight { get; set; } = "Away From Home";

    [MaxLength(600)]
    public string HeroDescription { get; set; } = "Modern, safe, and comfortable accommodation for students and working professionals.";

    [MaxLength(900)]
    public string HeroImageUrl { get; set; } = string.Empty;

    [MaxLength(900)]
    public string LogoImageUrl { get; set; } = string.Empty;

    [MaxLength(7)]
    public string PrimaryColor { get; set; } = "#263f95";

    [MaxLength(7)]
    public string AccentColor { get; set; } = "#e57942";

    [MaxLength(800)]
    public string AboutDescription { get; set; } = string.Empty;

    [MaxLength(600)]
    public string FooterDescription { get; set; } = string.Empty;

    public int FoundedYear { get; set; } = DateTime.Today.Year;

    [MaxLength(600)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ContactPhone { get; set; } = string.Empty;

    [MaxLength(80)]
    public string WhatsAppPhone { get; set; } = string.Empty;

    [MaxLength(180)]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(120)]
    public string UpiId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string UpiPayeeName { get; set; } = "HostelPro";

    public bool PaymentTestMode { get; set; } = true;
    public int BillingDay { get; set; } = 1;
    public int DueDay { get; set; } = 5;
    public decimal LateFee { get; set; } = 100;

    [MaxLength(1200)]
    public string HostelRules { get; set; } = "Visitors allowed only during 10 AM - 8 PM.\nMaintain silence after 10 PM.\nSmoking and alcohol strictly prohibited.";

    [MaxLength(40)]
    public string PaymentGatewayProvider { get; set; } = "manual";

    public bool PaymentGatewayEnabled { get; set; }

    public bool PublicRegistrationEnabled { get; set; } = true;
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(120)]
    public string Actor { get; set; } = "system";

    [MaxLength(80)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Entity { get; set; } = string.Empty;

    [MaxLength(80)]
    public string EntityId { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
