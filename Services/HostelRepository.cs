using HostelPro.Data;
using HostelPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;

namespace HostelPro.Services;

public sealed class HostelRepository(ApplicationDbContext db, IConfiguration configuration) : IHostelRepository
{
    public async Task<HostelSetting> GetSettingsAsync()
    {
        return await db.HostelSettings.AsNoTracking().OrderBy(setting => setting.Id).FirstAsync();
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        await ApplyLateFeesAsync("system");
        var today = DateTime.UtcNow.Date;
        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;
        var rooms = await db.Rooms.AsNoTracking().ToListAsync();
        var payments = await db.Payments.AsNoTracking().ToListAsync();
        var tenants = await db.Tenants.AsNoTracking().ToListAsync();
        var expenses = await db.Expenses.AsNoTracking().ToListAsync();

        return new DashboardStats
        {
            TotalRooms = rooms.Count,
            TotalBeds = rooms.Sum(room => room.TotalBeds),
            OccupiedBeds = rooms.Sum(room => room.OccupiedBeds),
            AvailableBeds = rooms.Sum(room => Math.Max(room.TotalBeds - room.OccupiedBeds, 0)),
            ActiveTenants = tenants.Count(tenant => tenant.Status == "active"),
            PendingBills = await db.Bills.CountAsync(bill => bill.Status == "pending" || bill.Status == "overdue"),
            MonthlyRevenue = payments
                .Where(payment => payment.CreatedUtc.Month == currentMonth && payment.CreatedUtc.Year == currentYear && payment.PaymentStatus == "success")
                .Sum(payment => payment.PaymentAmount),
            PaymentsToday = payments
                .Where(payment => payment.CreatedUtc.Date == today && payment.PaymentStatus == "success")
                .Sum(payment => payment.PaymentAmount),
            ExpectedRevenue = tenants
                .Where(tenant => tenant.Status == "active")
                .Sum(tenant => tenant.MonthlyRent),
            MonthlyExpenses = expenses
                .Where(expense => expense.ExpenseDate.Month == currentMonth && expense.ExpenseDate.Year == currentYear)
                .Sum(expense => expense.Amount),
            NewEnquiries = await db.Enquiries.CountAsync(enquiry => enquiry.Status == "new"),
            OverdueBills = await db.Bills.CountAsync(bill => bill.Status == "overdue"),
            OpenMaintenanceTickets = await db.MaintenanceTickets.CountAsync(ticket => ticket.Status != "completed" && ticket.Status != "cancelled"),
            PendingKycDocuments = await db.TenantDocuments.CountAsync(document => document.Status == "pending")
        };
    }

    public Task<List<Room>> GetPublicRoomsAsync()
    {
        return db.Rooms
            .AsNoTracking()
            .Include(room => room.Beds)
            .Where(room => room.Status == "active")
            .OrderBy(room => room.FloorNumber)
            .ThenBy(room => room.RoomNumber)
            .ToListAsync();
    }

    public Task<List<Room>> GetRoomsAsync()
    {
        return db.Rooms
            .AsNoTracking()
            .Include(room => room.Beds)
            .OrderBy(room => room.RoomNumber)
            .ToListAsync();
    }

    public async Task SaveRoomAsync(RoomForm form, string actor)
    {
        var room = form.Id.HasValue
            ? await db.Rooms.FirstAsync(room => room.Id == form.Id.Value)
            : new Room();

        room.RoomNumber = form.RoomNumber.Trim();
        room.FloorNumber = form.FloorNumber.Trim();
        room.RoomType = form.RoomType;
        room.TotalBeds = form.TotalBeds;
        room.MonthlyRent = form.MonthlyRent;
        room.Status = form.Status;
        room.Amenities = form.Amenities.Trim();
        room.Description = form.Description.Trim();
        room.CoverImageUrl = NormalizeImageUrl(form.CoverImageUrl);
        room.UpdatedUtc = DateTime.UtcNow;

        if (!form.Id.HasValue)
        {
            db.Rooms.Add(room);
        }

        await AddAuditAsync(actor, form.Id.HasValue ? "updated_room" : "created_room", "Room", room.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task DeleteRoomAsync(Guid roomId, string actor)
    {
        var room = await db.Rooms.Include(room => room.Beds).FirstAsync(room => room.Id == roomId);
        db.Beds.RemoveRange(room.Beds);
        db.Rooms.Remove(room);
        await AddAuditAsync(actor, "deleted_room", "Room", roomId.ToString());
        await db.SaveChangesAsync();
    }

    public Task<List<Bed>> GetBedsAsync()
    {
        return db.Beds
            .AsNoTracking()
            .Include(bed => bed.Room)
            .OrderBy(bed => bed.Room!.RoomNumber)
            .ThenBy(bed => bed.BedNumber)
            .ToListAsync();
    }

    public async Task SaveBedAsync(BedForm form, string actor)
    {
        var bed = new Bed
        {
            RoomId = form.RoomId,
            BedNumber = form.BedNumber.Trim(),
            Status = form.Status
        };

        db.Beds.Add(bed);
        await AddAuditAsync(actor, "created_bed", "Bed", bed.Id.ToString());
        await db.SaveChangesAsync();
        await RecalculateRoomOccupancyAsync(form.RoomId);
    }

    public async Task DeleteBedAsync(Guid bedId, string actor)
    {
        var bed = await db.Beds.FirstAsync(bed => bed.Id == bedId);
        var roomId = bed.RoomId;
        db.Beds.Remove(bed);
        await AddAuditAsync(actor, "deleted_bed", "Bed", bedId.ToString());
        await db.SaveChangesAsync();
        await RecalculateRoomOccupancyAsync(roomId);
    }

    public Task<List<Tenant>> GetTenantsAsync()
    {
        return db.Tenants
            .AsNoTracking()
            .Include(tenant => tenant.Room)
            .Include(tenant => tenant.Bed)
            .OrderBy(tenant => tenant.FullName)
            .ToListAsync();
    }

    public async Task SaveTenantAsync(TenantForm form, string actor)
    {
        await EnsureResidentCategoryAsync(form.Gender);
        var tenant = form.Id.HasValue
            ? await db.Tenants.FirstAsync(tenant => tenant.Id == form.Id.Value)
            : new Tenant();

        var previousRoomId = tenant.RoomId;
        var previousBedId = tenant.BedId;

        tenant.FullName = form.FullName.Trim();
        tenant.Email = form.Email.Trim();
        tenant.Phone = form.Phone.Trim();
        tenant.Gender = form.Gender;
        tenant.RoomId = form.RoomId;
        tenant.BedId = form.BedId;
        tenant.MonthlyRent = form.MonthlyRent;
        tenant.JoiningDate = form.JoiningDate;
        tenant.EmergencyContact = form.EmergencyContact.Trim();
        tenant.KycStatus = form.KycStatus;
        tenant.Status = form.Status;
        tenant.SecurityDepositAmount = form.SecurityDepositAmount;
        tenant.NoticePeriodDays = form.NoticePeriodDays;

        if (!form.Id.HasValue)
        {
            tenant.SecurityDepositStatus = "pending";
            db.Tenants.Add(tenant);
            db.Bills.Add(CreateSecurityDepositBill(tenant.Id, form.SecurityDepositAmount));
        }
        else if (tenant.SecurityDepositStatus is "pending" or "partially_paid")
        {
            var depositBill = await db.Bills.FirstOrDefaultAsync(bill =>
                bill.TenantId == tenant.Id && bill.BillMonth == "Security Deposit" && bill.Status != "paid");
            if (depositBill is not null)
            {
                depositBill.RentAmount = form.SecurityDepositAmount;
            }
        }

        if (previousBedId.HasValue && previousBedId != form.BedId)
        {
            var previousBed = await db.Beds.FirstOrDefaultAsync(bed => bed.Id == previousBedId.Value);
            if (previousBed is not null)
            {
                previousBed.Status = "available";
                previousBed.TenantId = null;
            }
        }

        if (form.BedId.HasValue)
        {
            var bed = await db.Beds.FirstAsync(bed => bed.Id == form.BedId.Value);
            bed.Status = tenant.Status == "active" ? "occupied" : "available";
            bed.TenantId = tenant.Status == "active" ? tenant.Id : null;
        }

        await AddAuditAsync(actor, form.Id.HasValue ? "updated_tenant" : "created_tenant", "Tenant", tenant.Id.ToString());
        await db.SaveChangesAsync();
        await RecalculateRoomOccupancyAsync(form.RoomId);
        if (form.Id.HasValue && previousRoomId != Guid.Empty && previousRoomId != form.RoomId)
        {
            await RecalculateRoomOccupancyAsync(previousRoomId);
        }
    }

    public async Task StartTenantNoticeAsync(TenantNoticeForm form, string actor)
    {
        var tenant = await db.Tenants.FirstAsync(item => item.Id == form.TenantId);
        if (tenant.Status != "active")
        {
            throw new InvalidOperationException("Only active tenants can start a notice period.");
        }

        var earliestDate = DateOnly.FromDateTime(DateTime.Today.AddDays(tenant.NoticePeriodDays));
        if (form.PlannedVacateDate < earliestDate)
        {
            throw new InvalidOperationException($"This tenant requires {tenant.NoticePeriodDays} days notice. Earliest vacate date is {earliestDate:dd MMM yyyy}.");
        }

        tenant.Status = "notice_period";
        tenant.NoticeGivenDate = DateOnly.FromDateTime(DateTime.Today);
        tenant.PlannedVacateDate = form.PlannedVacateDate;
        await AddAuditAsync(actor, "started_vacate_notice", "Tenant", tenant.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task<decimal> VacateTenantAsync(TenantVacateForm form, string actor)
    {
        var tenant = await db.Tenants.FirstAsync(item => item.Id == form.TenantId);
        if (tenant.Status != "notice_period" || !tenant.PlannedVacateDate.HasValue)
        {
            throw new InvalidOperationException("Start the tenant notice period before completing vacate.");
        }

        if (tenant.PlannedVacateDate.Value > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new InvalidOperationException($"Vacate can be completed on or after {tenant.PlannedVacateDate.Value:dd MMM yyyy}.");
        }

        var refundable = Math.Max(tenant.SecurityDepositPaidAmount - form.Deductions, 0);
        tenant.Status = "vacated";
        tenant.VacatedDate = DateOnly.FromDateTime(DateTime.Today);
        tenant.SecurityDepositDeductions = Math.Min(form.Deductions, tenant.SecurityDepositPaidAmount);
        tenant.SecurityDepositRefundedAmount = refundable;
        tenant.SecurityDepositStatus = refundable <= 0
            ? "forfeited"
            : tenant.SecurityDepositDeductions > 0 ? "partially_refunded" : "refunded";
        tenant.VacateNotes = form.Notes.Trim();

        if (tenant.BedId.HasValue)
        {
            var bed = await db.Beds.FirstAsync(bed => bed.Id == tenant.BedId.Value);
            bed.Status = "available";
            bed.TenantId = null;
        }

        await AddAuditAsync(actor, "vacated_tenant", "Tenant", tenant.Id.ToString());
        await db.SaveChangesAsync();
        await RecalculateRoomOccupancyAsync(tenant.RoomId);
        return refundable;
    }

    public Task<List<TenantApplication>> GetTenantApplicationsAsync()
    {
        return db.TenantApplications
            .AsNoTracking()
            .Include(application => application.Tenant)
            .Include(application => application.PreferredRoom)
            .Include(application => application.PreferredBed)
            .OrderBy(application => application.Status == "pending" ? 0 : 1)
            .ThenByDescending(application => application.SubmittedUtc)
            .ToListAsync();
    }

    public async Task<TenantApplication> SubmitTenantApplicationAsync(TenantApplicationForm form)
    {
        if (!string.IsNullOrWhiteSpace(form.Website))
        {
            throw new InvalidOperationException("Unable to submit this application.");
        }

        var settings = await db.HostelSettings.AsNoTracking().OrderBy(setting => setting.Id).FirstAsync();
        if (!settings.PublicRegistrationEnabled)
        {
            throw new InvalidOperationException("Online applications are currently closed.");
        }

        var inferredGender = settings.ResidentCategory switch
        {
            "men" => "male",
            "women" => "female",
            _ => "not_specified"
        };
        await EnsureResidentCategoryAsync(inferredGender);

        if (form.PreferredRoomType == "any")
        {
            throw new InvalidOperationException("Select an available room type.");
        }

        var room = await db.Rooms
            .Include(item => item.Beds)
            .Where(item => item.RoomType == form.PreferredRoomType && item.Status == "active")
            .OrderBy(item => item.MonthlyRent)
            .FirstOrDefaultAsync(item => item.Beds.Any(bed => bed.Status == "available"));
        if (room is null)
        {
            throw new InvalidOperationException("That room type is no longer available. Choose another option.");
        }

        var phone = form.Phone.Trim();
        var duplicateWindow = DateTime.UtcNow.AddMinutes(-15);
        if (await db.TenantApplications.AnyAsync(application =>
            application.Phone == phone && application.SubmittedUtc >= duplicateWindow))
        {
            throw new InvalidOperationException("An application for this phone number was submitted recently.");
        }

        var application = new TenantApplication
        {
            ApplicationNumber = $"APP{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetHexString(3)}",
            FullName = form.FullName.Trim(),
            Email = form.Email.Trim(),
            Phone = phone,
            Gender = inferredGender,
            PreferredRoomType = room.RoomType,
            PreferredRoomId = null,
            PreferredBedId = null,
            RoomPrice = room.MonthlyRent,
            AdvanceAmount = 0,
            Occupation = form.Occupation.Trim(),
            EmergencyContact = form.EmergencyContact.Trim(),
            Notes = form.Notes.Trim()
        };

        db.TenantApplications.Add(application);
        await AddAuditAsync("public", "submitted_tenant_application", "TenantApplication", application.Id.ToString());
        await db.SaveChangesAsync();
        return application;
    }

    public async Task<Tenant> ApproveTenantApplicationAsync(TenantApplicationApprovalForm form, string actor)
    {
        var application = await db.TenantApplications.FirstAsync(item => item.Id == form.ApplicationId);
        if (application.Status != "pending")
        {
            throw new InvalidOperationException("Only pending applications can be approved.");
        }

        var room = await db.Rooms.FirstAsync(item => item.Id == form.RoomId && item.Status == "active");
        Bed? bed = null;
        if (form.BedId.HasValue)
        {
            bed = await db.Beds.FirstAsync(item => item.Id == form.BedId.Value && item.RoomId == room.Id);
            if (bed.Status != "available")
            {
                throw new InvalidOperationException("The selected bed is no longer available.");
            }
        }

        await EnsureResidentCategoryAsync(application.Gender);
        var tenant = new Tenant
        {
            FullName = application.FullName,
            Email = application.Email,
            Phone = application.Phone,
            Gender = application.Gender,
            RoomId = room.Id,
            BedId = bed?.Id,
            MonthlyRent = form.MonthlyRent,
            JoiningDate = form.JoiningDate,
            SecurityDepositAmount = form.SecurityDepositAmount,
            SecurityDepositStatus = "pending",
            NoticePeriodDays = form.NoticePeriodDays,
            EmergencyContact = application.EmergencyContact,
            KycStatus = "pending",
            Status = "active"
        };

        db.Tenants.Add(tenant);
        if (bed is not null)
        {
            bed.Status = "occupied";
            bed.TenantId = tenant.Id;
        }

        application.Status = "approved";
        application.TenantId = tenant.Id;
        application.ReviewedUtc = DateTime.UtcNow;
        application.ReviewedBy = actor;

        db.Bills.Add(CreateSecurityDepositBill(tenant.Id, form.SecurityDepositAmount));

        await AddAuditAsync(actor, "approved_tenant_application", "TenantApplication", application.Id.ToString());
        await db.SaveChangesAsync();
        await RecalculateRoomOccupancyAsync(room.Id);
        return tenant;
    }

    public async Task UpdateTenantApplicationStatusAsync(Guid applicationId, string status, string actor)
    {
        if (status is not ("rejected" or "pending"))
        {
            throw new InvalidOperationException("Unsupported application status.");
        }

        var application = await db.TenantApplications.FirstAsync(item => item.Id == applicationId);
        if (application.Status == "approved")
        {
            throw new InvalidOperationException("Approved applications cannot be changed.");
        }

        application.Status = status;
        application.ReviewedUtc = status == "pending" ? null : DateTime.UtcNow;
        application.ReviewedBy = status == "pending" ? string.Empty : actor;
        await AddAuditAsync(actor, $"{status}_tenant_application", "TenantApplication", application.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task<List<Bill>> GetBillsAsync()
    {
        await ApplyLateFeesAsync("system");
        return await db.Bills
            .AsNoTracking()
            .Include(bill => bill.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .OrderByDescending(bill => bill.BillYear)
            .ThenByDescending(bill => bill.DueDate)
            .ToListAsync();
    }

    public async Task SaveBillAsync(BillForm form, string actor)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstAsync(item => item.Id == form.TenantId);
        if (form.BillMonth != "Security Deposit" && tenant.SecurityDepositStatus != "paid")
        {
            throw new InvalidOperationException("The security deposit must be paid before adding hostel-fee bills.");
        }

        var bill = new Bill
        {
            TenantId = form.TenantId,
            BillMonth = form.BillMonth,
            BillYear = form.BillYear,
            RentAmount = form.RentAmount,
            ElectricityCharges = form.ElectricityCharges,
            MaintenanceCharges = form.MaintenanceCharges,
            OtherCharges = form.OtherCharges,
            Discount = form.Discount,
            LateFeeAmount = form.LateFeeAmount,
            DueDate = form.DueDate,
            InvoiceNumber = CreateInvoiceNumber(),
            Status = "pending"
        };

        db.Bills.Add(bill);
        await AddAuditAsync(actor, "created_bill", "Bill", bill.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task<int> AutoGenerateBillsAsync(string actor)
    {
        var tenants = await db.Tenants
            .Where(tenant => tenant.Status == "active" && tenant.SecurityDepositStatus == "paid")
            .ToListAsync();
        var settings = await db.HostelSettings.AsNoTracking().OrderBy(setting => setting.Id).FirstAsync();
        var month = DateTime.Today.ToString("MMMM");
        var year = DateTime.Today.Year;
        var created = 0;
        var dueDay = Math.Clamp(settings.DueDay, 1, DateTime.DaysInMonth(year, DateTime.Today.Month));
        var dueDate = new DateOnly(year, DateTime.Today.Month, dueDay);

        foreach (var tenant in tenants)
        {
            var exists = await db.Bills.AnyAsync(bill =>
                bill.TenantId == tenant.Id &&
                bill.BillMonth == month &&
                bill.BillYear == year);

            if (exists)
            {
                continue;
            }

            db.Bills.Add(new Bill
            {
                TenantId = tenant.Id,
                BillMonth = month,
                BillYear = year,
                RentAmount = tenant.MonthlyRent,
                DueDate = dueDate,
                InvoiceNumber = CreateInvoiceNumber()
            });
            created++;
        }

        await AddAuditAsync(actor, "auto_generated_bills", "Bill", created.ToString());
        await db.SaveChangesAsync();
        return created;
    }

    public async Task MarkInvoiceSentAsync(Guid billId, string actor)
    {
        var bill = await db.Bills.FirstAsync(bill => bill.Id == billId);
        bill.InvoiceSentUtc = DateTime.UtcNow;
        await AddAuditAsync(actor, "sent_invoice", "Bill", billId.ToString());
        await db.SaveChangesAsync();
    }

    public Task<List<Payment>> GetPaymentsAsync()
    {
        return db.Payments
            .AsNoTracking()
            .Include(payment => payment.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .Include(payment => payment.Bill)
            .OrderByDescending(payment => payment.CreatedUtc)
            .ToListAsync();
    }

    public async Task<Payment> RecordPaymentAsync(PaymentForm form, string actor)
    {
        var bill = await db.Bills.Include(bill => bill.Tenant).FirstAsync(bill => bill.Id == form.BillId);
        if (bill.BillMonth != "Security Deposit" && bill.Tenant?.SecurityDepositStatus != "paid")
        {
            throw new InvalidOperationException("Pay the tenant's security deposit before recording hostel-fee payments.");
        }

        var settings = await db.HostelSettings.AsNoTracking().OrderBy(setting => setting.Id).FirstAsync();
        var isTest = settings.PaymentTestMode;
        var payment = new Payment
        {
            BillId = bill.Id,
            TenantId = bill.TenantId,
            ReceiptNumber = $"RCP{DateTime.UtcNow:yyyyMMddHHmmss}-{RandomNumberGenerator.GetHexString(2)}",
            PaymentAmount = form.PaymentAmount,
            PaymentMethod = form.PaymentMethod,
            GatewayProvider = string.Empty,
            TransactionId = string.IsNullOrWhiteSpace(form.TransactionId)
                ? $"{form.PaymentMethod.ToUpperInvariant()}-{DateTime.UtcNow:yyyyMMddHHmmss}-{RandomNumberGenerator.GetHexString(2)}"
                : form.TransactionId.Trim(),
            PaymentStatus = isTest ? "test" : "success",
            Tenant = bill.Tenant,
            Bill = bill
        };

        if (!isTest)
        {
            bill.PaidAmount += form.PaymentAmount;
            bill.Status = bill.PaidAmount >= bill.TotalAmount ? "paid" : "partially_paid";
            if (bill.BillMonth == "Security Deposit" && bill.Tenant is not null)
            {
                bill.Tenant.SecurityDepositPaidAmount = Math.Min(bill.PaidAmount, bill.TotalAmount);
                bill.Tenant.SecurityDepositStatus = bill.Status == "paid" ? "paid" : "partially_paid";
            }
        }

        db.Payments.Add(payment);
        await AddAuditAsync(actor, "recorded_payment", "Payment", payment.Id.ToString());
        await db.SaveChangesAsync();
        return payment;
    }

    public Task<List<PaymentLink>> GetPaymentLinksAsync()
    {
        return db.PaymentLinks
            .AsNoTracking()
            .Include(link => link.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .Include(link => link.Bill)
            .Include(link => link.Attempts)
            .OrderByDescending(link => link.CreatedUtc)
            .ToListAsync();
    }

    public async Task<CreatedPaymentLink> CreatePaymentLinkAsync(Guid billId, string actor)
    {
        var bill = await db.Bills.Include(item => item.Tenant).FirstAsync(item => item.Id == billId);
        if (bill.BillMonth != "Security Deposit" && bill.Tenant?.SecurityDepositStatus != "paid")
        {
            throw new InvalidOperationException("Create and settle the security-deposit payment link first.");
        }

        var amount = Math.Max(bill.TotalAmount - bill.PaidAmount, 0);
        if (amount <= 0 || bill.Status is "paid" or "cancelled")
        {
            throw new InvalidOperationException("This bill has no payable balance.");
        }

        var oldLinks = await db.PaymentLinks
            .Where(link => link.BillId == billId && link.Status == "active")
            .ToListAsync();
        foreach (var oldLink in oldLinks)
        {
            oldLink.Status = "revoked";
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var link = new PaymentLink
        {
            BillId = bill.Id,
            TenantId = bill.TenantId,
            TokenHash = HashToken(token),
            Amount = amount,
            ExpiresUtc = DateTime.UtcNow.AddDays(7),
            CreatedBy = actor
        };

        db.PaymentLinks.Add(link);
        await AddAuditAsync(actor, "created_payment_link", "PaymentLink", link.Id.ToString());
        await db.SaveChangesAsync();
        return new CreatedPaymentLink(link.Id, token, link.ExpiresUtc);
    }

    public async Task<PaymentLink?> GetPaymentLinkByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
        {
            return null;
        }

        var tokenHash = HashToken(token);
        var link = await db.PaymentLinks
            .AsNoTracking()
            .Include(item => item.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .Include(item => item.Tenant)
            .ThenInclude(tenant => tenant!.Bed)
            .Include(item => item.Bill)
            .Include(item => item.Attempts)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash);

        if (link is not null && link.Status == "active" && link.ExpiresUtc <= DateTime.UtcNow)
        {
            var tracked = await db.PaymentLinks.FirstAsync(item => item.Id == link.Id);
            tracked.Status = "expired";
            await db.SaveChangesAsync();
            link.Status = "expired";
        }

        return link;
    }

    public async Task<PaymentAttempt> SubmitPaymentAttemptAsync(string token, PublicPaymentAttemptForm form)
    {
        var tokenHash = HashToken(token);
        var link = await db.PaymentLinks
            .Include(item => item.Attempts)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash);
        if (link is null || link.Status != "active" || link.ExpiresUtc <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("This payment link is no longer valid.");
        }

        if (link.Attempts.Any(item => item.Status == "pending"))
        {
            throw new InvalidOperationException("A payment confirmation is already awaiting verification.");
        }

        var transactionId = form.TransactionId.Trim();
        if (await db.PaymentAttempts.AnyAsync(item => item.TransactionId == transactionId && item.Status != "rejected"))
        {
            throw new InvalidOperationException("This transaction reference has already been submitted.");
        }

        var attempt = new PaymentAttempt
        {
            PaymentLinkId = link.Id,
            Amount = link.Amount,
            PaymentMethod = "upi",
            TransactionId = transactionId
        };
        db.PaymentAttempts.Add(attempt);
        await AddAuditAsync("public", "submitted_payment_confirmation", "PaymentAttempt", attempt.Id.ToString());
        await db.SaveChangesAsync();
        return attempt;
    }

    public Task<List<PaymentAttempt>> GetPendingPaymentAttemptsAsync()
    {
        return db.PaymentAttempts
            .AsNoTracking()
            .Include(attempt => attempt.PaymentLink)
            .ThenInclude(link => link!.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .Include(attempt => attempt.PaymentLink)
            .ThenInclude(link => link!.Bill)
            .Where(attempt => attempt.Status == "pending")
            .OrderBy(attempt => attempt.SubmittedUtc)
            .ToListAsync();
    }

    public async Task<Payment> ReviewPaymentAttemptAsync(Guid attemptId, bool approve, string actor)
    {
        var attempt = await db.PaymentAttempts
            .Include(item => item.PaymentLink)
            .FirstAsync(item => item.Id == attemptId);
        if (attempt.Status != "pending" || attempt.PaymentLink is null)
        {
            throw new InvalidOperationException("This payment confirmation has already been reviewed.");
        }

        attempt.Status = approve ? "verified" : "rejected";
        attempt.ReviewedUtc = DateTime.UtcNow;
        attempt.ReviewedBy = actor;
        if (!approve)
        {
            await AddAuditAsync(actor, "rejected_payment_confirmation", "PaymentAttempt", attempt.Id.ToString());
            await db.SaveChangesAsync();
            return new Payment { PaymentStatus = "rejected" };
        }

        var payment = await RecordPaymentAsync(new PaymentForm
        {
            BillId = attempt.PaymentLink.BillId,
            PaymentAmount = attempt.Amount,
            PaymentMethod = attempt.PaymentMethod,
            TransactionId = attempt.TransactionId
        }, actor);
        attempt.PaymentLink.Status = "paid";
        attempt.PaymentLink.PaidUtc = DateTime.UtcNow;
        await AddAuditAsync(actor, "verified_payment_confirmation", "PaymentAttempt", attempt.Id.ToString());
        await db.SaveChangesAsync();
        return payment;
    }

    public async Task RevokePaymentLinkAsync(Guid paymentLinkId, string actor)
    {
        var link = await db.PaymentLinks.FirstAsync(item => item.Id == paymentLinkId);
        if (link.Status == "active")
        {
            link.Status = "revoked";
            await AddAuditAsync(actor, "revoked_payment_link", "PaymentLink", link.Id.ToString());
            await db.SaveChangesAsync();
        }
    }

    public async Task<Payment> RecordGatewayPaymentAsync(PaymentWebhookRequest request)
    {
        if (!request.Status.Equals("paid", StringComparison.OrdinalIgnoreCase)
            && !request.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The provider has not confirmed this payment.");
        }

        var existing = await db.Payments
            .AsNoTracking()
            .Include(payment => payment.Tenant)
            .Include(payment => payment.Bill)
            .FirstOrDefaultAsync(payment => payment.TransactionId == request.TransactionId.Trim());
        if (existing is not null)
        {
            return existing;
        }

        var bill = await db.Bills.FirstOrDefaultAsync(item => item.InvoiceNumber == request.InvoiceNumber.Trim())
            ?? throw new InvalidOperationException("Invoice not found.");
        var link = await db.PaymentLinks.FirstOrDefaultAsync(item =>
            item.BillId == bill.Id && item.Status == "active" && item.ExpiresUtc > DateTime.UtcNow)
            ?? throw new InvalidOperationException("No active payment link exists for this invoice.");
        if (request.Amount != link.Amount)
        {
            throw new InvalidOperationException("Payment amount does not match the active payment link.");
        }

        var settings = await db.HostelSettings.AsNoTracking().OrderBy(setting => setting.Id).FirstAsync();
        if (settings.PaymentTestMode)
        {
            throw new InvalidOperationException("Live gateway callbacks are disabled in payment test mode.");
        }

        var payment = await RecordPaymentAsync(new PaymentForm
        {
            BillId = bill.Id,
            PaymentAmount = request.Amount,
            PaymentMethod = "online",
            TransactionId = request.TransactionId
        }, $"gateway:{request.Provider.Trim().ToLowerInvariant()}");
        payment.GatewayProvider = request.Provider.Trim().ToLowerInvariant();
        link.Status = "paid";
        link.PaidUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return payment;
    }

    public Task<List<Expense>> GetExpensesAsync()
    {
        return db.Expenses
            .AsNoTracking()
            .OrderByDescending(expense => expense.ExpenseDate)
            .ThenByDescending(expense => expense.CreatedUtc)
            .ToListAsync();
    }

    public async Task SaveExpenseAsync(ExpenseForm form, string actor)
    {
        var expense = form.Id.HasValue
            ? await db.Expenses.FirstAsync(expense => expense.Id == form.Id.Value)
            : new Expense();

        expense.Category = form.Category.Trim();
        expense.Title = form.Title.Trim();
        expense.Amount = form.Amount;
        expense.ExpenseDate = form.ExpenseDate;
        expense.Notes = form.Notes.Trim();

        if (!form.Id.HasValue)
        {
            db.Expenses.Add(expense);
        }

        await AddAuditAsync(actor, form.Id.HasValue ? "updated_expense" : "created_expense", "Expense", expense.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task DeleteExpenseAsync(Guid expenseId, string actor)
    {
        var expense = await db.Expenses.FirstAsync(expense => expense.Id == expenseId);
        db.Expenses.Remove(expense);
        await AddAuditAsync(actor, "deleted_expense", "Expense", expenseId.ToString());
        await db.SaveChangesAsync();
    }

    public Task<List<TenantDocument>> GetTenantDocumentsAsync(Guid? tenantId = null)
    {
        var query = db.TenantDocuments
            .AsNoTracking()
            .Include(document => document.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(document => document.TenantId == tenantId.Value);
        }

        return query
            .OrderByDescending(document => document.UploadedUtc)
            .ToListAsync();
    }

    public async Task SaveTenantDocumentAsync(TenantDocumentForm form, string actor)
    {
        var document = form.Id.HasValue
            ? await db.TenantDocuments.FirstAsync(document => document.Id == form.Id.Value)
            : new TenantDocument();

        document.TenantId = form.TenantId;
        document.DocumentType = form.DocumentType.Trim();
        document.FileUrl = form.FileUrl.Trim();
        document.Status = form.Status;
        document.Notes = form.Notes.Trim();

        if (document.Status is "approved" or "rejected")
        {
            document.ReviewedUtc = DateTime.UtcNow;
        }

        if (!form.Id.HasValue)
        {
            db.TenantDocuments.Add(document);
        }

        await AddAuditAsync(actor, form.Id.HasValue ? "updated_tenant_document" : "created_tenant_document", "TenantDocument", document.Id.ToString());
        await db.SaveChangesAsync();
        await UpdateTenantKycStatusAsync(document.TenantId);
        await db.SaveChangesAsync();
    }

    public async Task UpdateTenantDocumentStatusAsync(Guid documentId, string status, string actor)
    {
        var document = await db.TenantDocuments.FirstAsync(document => document.Id == documentId);
        document.Status = status;
        document.ReviewedUtc = DateTime.UtcNow;
        await AddAuditAsync(actor, "reviewed_tenant_document", "TenantDocument", documentId.ToString());
        await db.SaveChangesAsync();
        await UpdateTenantKycStatusAsync(document.TenantId);
        await db.SaveChangesAsync();
    }

    public Task<List<MaintenanceTicket>> GetMaintenanceTicketsAsync(Guid? tenantId = null)
    {
        var query = db.MaintenanceTickets
            .AsNoTracking()
            .Include(ticket => ticket.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .Include(ticket => ticket.Room)
            .AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(ticket => ticket.TenantId == tenantId.Value);
        }

        return query
            .OrderBy(ticket => ticket.Status == "completed" || ticket.Status == "cancelled")
            .ThenByDescending(ticket => ticket.CreatedUtc)
            .ToListAsync();
    }

    public async Task SaveMaintenanceTicketAsync(MaintenanceTicketForm form, string actor)
    {
        var ticket = form.Id.HasValue
            ? await db.MaintenanceTickets.FirstAsync(ticket => ticket.Id == form.Id.Value)
            : new MaintenanceTicket();

        ticket.TenantId = form.TenantId;
        ticket.RoomId = form.RoomId;
        ticket.Title = form.Title.Trim();
        ticket.Description = form.Description.Trim();
        ticket.Priority = form.Priority;
        ticket.Status = form.Status;
        ticket.AssignedTo = form.AssignedTo.Trim();
        ticket.ResolutionNotes = form.ResolutionNotes.Trim();
        ticket.UpdatedUtc = DateTime.UtcNow;
        ticket.CompletedUtc = form.Status == "completed" ? DateTime.UtcNow : null;

        if (!form.Id.HasValue)
        {
            db.MaintenanceTickets.Add(ticket);
        }

        await AddAuditAsync(actor, form.Id.HasValue ? "updated_maintenance_ticket" : "created_maintenance_ticket", "MaintenanceTicket", ticket.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task DeleteMaintenanceTicketAsync(Guid ticketId, string actor)
    {
        var ticket = await db.MaintenanceTickets.FirstAsync(ticket => ticket.Id == ticketId);
        db.MaintenanceTickets.Remove(ticket);
        await AddAuditAsync(actor, "deleted_maintenance_ticket", "MaintenanceTicket", ticketId.ToString());
        await db.SaveChangesAsync();
    }

    public Task<List<MessMenu>> GetMessMenusAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var query = db.MessMenus.AsNoTracking().AsQueryable();
        if (from.HasValue)
        {
            query = query.Where(menu => menu.MenuDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(menu => menu.MenuDate <= to.Value);
        }

        return query
            .OrderBy(menu => menu.MenuDate)
            .ThenBy(menu => menu.MealType)
            .ToListAsync();
    }

    public Task<List<MessAttendance>> GetMessAttendancesAsync(Guid tenantId, DateOnly? from = null, DateOnly? to = null)
    {
        var query = db.MessAttendances
            .AsNoTracking()
            .Where(attendance => attendance.TenantId == tenantId);

        if (from.HasValue)
        {
            query = query.Where(attendance => attendance.MealDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(attendance => attendance.MealDate <= to.Value);
        }

        return query
            .OrderBy(attendance => attendance.MealDate)
            .ThenBy(attendance => attendance.MealType)
            .ToListAsync();
    }

    public async Task SaveMessMenuAsync(MessMenuForm form, string actor)
    {
        var menu = form.Id.HasValue
            ? await db.MessMenus.FirstAsync(menu => menu.Id == form.Id.Value)
            : await db.MessMenus.FirstOrDefaultAsync(menu => menu.MenuDate == form.MenuDate && menu.MealType == form.MealType) ?? new MessMenu();

        var isNew = menu.Id == Guid.Empty || !await db.MessMenus.AnyAsync(existing => existing.Id == menu.Id);
        menu.MenuDate = form.MenuDate;
        menu.MealType = form.MealType;
        menu.MenuItems = form.MenuItems.Trim();
        menu.OptInCount = form.OptInCount;
        menu.OptOutCount = form.OptOutCount;

        if (isNew)
        {
            db.MessMenus.Add(menu);
        }

        await AddAuditAsync(actor, isNew ? "created_mess_menu" : "updated_mess_menu", "MessMenu", menu.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task DeleteMessMenuAsync(Guid menuId, string actor)
    {
        var menu = await db.MessMenus.FirstAsync(menu => menu.Id == menuId);
        db.MessMenus.Remove(menu);
        await AddAuditAsync(actor, "deleted_mess_menu", "MessMenu", menuId.ToString());
        await db.SaveChangesAsync();
    }

    public async Task SaveMessAttendanceAsync(MessAttendanceForm form, string actor)
    {
        var attendance = await db.MessAttendances.FirstOrDefaultAsync(attendance =>
            attendance.TenantId == form.TenantId &&
            attendance.MealDate == form.MealDate &&
            attendance.MealType == form.MealType);

        if (attendance is null)
        {
            attendance = new MessAttendance
            {
                TenantId = form.TenantId,
                MealDate = form.MealDate,
                MealType = form.MealType
            };
            db.MessAttendances.Add(attendance);
        }

        attendance.Status = form.Status;
        await AddAuditAsync(actor, "saved_mess_attendance", "MessAttendance", attendance.Id.ToString());
        await db.SaveChangesAsync();
        await RecalculateMessCountsAsync(form.MealDate, form.MealType);
        await db.SaveChangesAsync();
    }

    public Task<Tenant?> GetTenantByEmailAsync(string email)
    {
        var normalized = email.Trim().ToUpperInvariant();
        return db.Tenants
            .AsNoTracking()
            .Include(tenant => tenant.Room)
            .Include(tenant => tenant.Bed)
            .FirstOrDefaultAsync(tenant => tenant.Email.ToUpper() == normalized);
    }

    public Task<Tenant?> GetTenantAsync(Guid tenantId)
    {
        return db.Tenants
            .AsNoTracking()
            .Include(tenant => tenant.Room)
            .Include(tenant => tenant.Bed)
            .FirstOrDefaultAsync(tenant => tenant.Id == tenantId);
    }

    public Task<List<Bill>> GetTenantBillsAsync(Guid tenantId)
    {
        return db.Bills
            .AsNoTracking()
            .Include(bill => bill.Tenant)
            .ThenInclude(tenant => tenant!.Room)
            .Where(bill => bill.TenantId == tenantId)
            .OrderByDescending(bill => bill.BillYear)
            .ThenByDescending(bill => bill.DueDate)
            .ToListAsync();
    }

    public Task<List<Payment>> GetTenantPaymentsAsync(Guid tenantId)
    {
        return db.Payments
            .AsNoTracking()
            .Include(payment => payment.Bill)
            .Where(payment => payment.TenantId == tenantId)
            .OrderByDescending(payment => payment.CreatedUtc)
            .ToListAsync();
    }

    public Task<List<Enquiry>> GetEnquiriesAsync()
    {
        return db.Enquiries
            .AsNoTracking()
            .OrderByDescending(enquiry => enquiry.CreatedUtc)
            .ToListAsync();
    }

    public async Task CreateEnquiryAsync(ContactForm form)
    {
        db.Enquiries.Add(new Enquiry
        {
            Name = form.Name.Trim(),
            Email = form.Email.Trim(),
            Phone = form.Phone.Trim(),
            Message = form.Message.Trim()
        });

        await db.SaveChangesAsync();
    }

    public async Task UpdateEnquiryStatusAsync(Guid enquiryId, string status, string actor)
    {
        var enquiry = await db.Enquiries.FirstAsync(enquiry => enquiry.Id == enquiryId);
        enquiry.Status = status;
        await AddAuditAsync(actor, "updated_enquiry", "Enquiry", enquiryId.ToString());
        await db.SaveChangesAsync();
    }

    public Task<List<GalleryImage>> GetGalleryAsync(bool publishedOnly = false)
    {
        var query = db.GalleryImages.AsNoTracking();
        if (publishedOnly)
        {
            query = query.Where(image => image.IsPublished);
        }

        return query.OrderBy(image => image.SortOrder).ToListAsync();
    }

    public async Task SaveGalleryImageAsync(GalleryForm form, string actor)
    {
        GalleryImage entity;
        if (form.Id.HasValue)
        {
            entity = await db.GalleryImages.FindAsync(form.Id.Value)
                     ?? throw new KeyNotFoundException("Gallery image not found");

            await AddAuditAsync(actor, "updated_gallery_image", "GalleryImage", entity.Id.ToString());
        }
        else
        {
            entity = new GalleryImage();
            db.GalleryImages.Add(entity);
            await AddAuditAsync(actor, "created_gallery_image", "GalleryImage", entity.Id.ToString());
        }

        entity.Album = form.Album.Trim();
        entity.Caption = form.Caption.Trim();
        entity.ImageUrl = NormalizeImageUrl(form.ImageUrl);
        entity.IsPublished = form.IsPublished;
        entity.SortOrder = form.SortOrder;

        await db.SaveChangesAsync();
    }

    public async Task DeleteGalleryImageAsync(Guid id, string actor)
    {
        var entity = await db.GalleryImages.FindAsync(id);
        if (entity is not null)
        {
            db.GalleryImages.Remove(entity);
            await AddAuditAsync(actor, "deleted_gallery_image", "GalleryImage", id.ToString());
            await db.SaveChangesAsync();
        }
    }

    public Task<List<Amenity>> GetAmenitiesAsync(bool publishedOnly = false)
    {
        var query = db.Amenities.AsNoTracking();
        if (publishedOnly)
        {
            query = query.Where(amenity => amenity.IsPublished);
        }

        return query.OrderBy(amenity => amenity.SortOrder).ThenBy(amenity => amenity.Name).ToListAsync();
    }

    public async Task SaveAmenityAsync(AmenityForm form, string actor)
    {
        var amenity = form.Id.HasValue
            ? await db.Amenities.FirstAsync(item => item.Id == form.Id.Value)
            : new Amenity();

        amenity.Name = form.Name.Trim();
        amenity.Description = form.Description.Trim();
        amenity.IconKey = form.IconKey.Trim().ToLowerInvariant();
        amenity.SortOrder = form.SortOrder;
        amenity.IsPublished = form.IsPublished;

        if (!form.Id.HasValue)
        {
            db.Amenities.Add(amenity);
        }

        await AddAuditAsync(actor, form.Id.HasValue ? "updated_amenity" : "created_amenity", "Amenity", amenity.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task DeleteAmenityAsync(Guid id, string actor)
    {
        var amenity = await db.Amenities.FirstAsync(item => item.Id == id);
        db.Amenities.Remove(amenity);
        await AddAuditAsync(actor, "deleted_amenity", "Amenity", id.ToString());
        await db.SaveChangesAsync();
    }

    public Task<List<Testimonial>> GetTestimonialsAsync(bool publishedOnly = false)
    {
        var query = db.Testimonials.AsNoTracking();
        if (publishedOnly)
        {
            query = query.Where(testimonial => testimonial.IsPublished);
        }

        return query.OrderBy(testimonial => testimonial.SortOrder).ThenBy(testimonial => testimonial.Name).ToListAsync();
    }

    public async Task SaveTestimonialAsync(TestimonialForm form, string actor)
    {
        var testimonial = form.Id.HasValue
            ? await db.Testimonials.FirstAsync(item => item.Id == form.Id.Value)
            : new Testimonial();

        testimonial.Name = form.Name.Trim();
        testimonial.Role = form.Role.Trim();
        testimonial.Quote = form.Quote.Trim();
        testimonial.Rating = Math.Clamp(form.Rating, 1, 5);
        testimonial.SortOrder = form.SortOrder;
        testimonial.IsPublished = form.IsPublished;

        if (!form.Id.HasValue)
        {
            db.Testimonials.Add(testimonial);
        }

        await AddAuditAsync(actor, form.Id.HasValue ? "updated_testimonial" : "created_testimonial", "Testimonial", testimonial.Id.ToString());
        await db.SaveChangesAsync();
    }

    public async Task DeleteTestimonialAsync(Guid id, string actor)
    {
        var testimonial = await db.Testimonials.FirstAsync(item => item.Id == id);
        db.Testimonials.Remove(testimonial);
        await AddAuditAsync(actor, "deleted_testimonial", "Testimonial", id.ToString());
        await db.SaveChangesAsync();
    }

    public Task<List<AuditLog>> GetAuditLogsAsync()
    {
        return db.AuditLogs.AsNoTracking().OrderByDescending(log => log.CreatedUtc).Take(100).ToListAsync();
    }

    public async Task SaveSettingsAsync(HostelSetting setting, string actor)
    {
        var current = await db.HostelSettings.OrderBy(setting => setting.Id).FirstAsync();
        current.HostelName = setting.HostelName.Trim();
        current.PropertyModel = setting.PropertyModel is "co_living" ? "co_living" : "pg";
        current.ResidentCategory = setting.ResidentCategory is "men" or "women" ? setting.ResidentCategory : "mixed";
        current.Tagline = setting.Tagline.Trim();
        current.HeroTitle = setting.HeroTitle.Trim();
        current.HeroHighlight = setting.HeroHighlight.Trim();
        current.HeroDescription = setting.HeroDescription.Trim();
        current.HeroImageUrl = NormalizeImageUrl(setting.HeroImageUrl);
        current.LogoImageUrl = NormalizeImageUrl(setting.LogoImageUrl);
        current.PrimaryColor = NormalizeColor(setting.PrimaryColor, "#263f95");
        current.AccentColor = NormalizeColor(setting.AccentColor, "#e57942");
        current.AboutDescription = setting.AboutDescription.Trim();
        current.FooterDescription = setting.FooterDescription.Trim();
        current.FoundedYear = Math.Clamp(setting.FoundedYear, 1900, DateTime.Today.Year);
        current.Address = setting.Address.Trim();
        current.ContactPhone = setting.ContactPhone.Trim();
        current.WhatsAppPhone = setting.WhatsAppPhone.Trim();
        current.ContactEmail = setting.ContactEmail.Trim();
        current.UpiId = setting.UpiId.Trim();
        current.UpiPayeeName = setting.UpiPayeeName.Trim();
        current.PaymentTestMode = setting.PaymentTestMode;
        current.BillingDay = Math.Clamp(setting.BillingDay, 1, 28);
        current.DueDay = Math.Clamp(setting.DueDay, 1, 28);
        current.LateFee = setting.LateFee;
        current.HostelRules = setting.HostelRules.Trim();
        current.PaymentGatewayProvider = "manual";
        current.PaymentGatewayEnabled = false;
        current.PublicRegistrationEnabled = setting.PublicRegistrationEnabled;

        await AddAuditAsync(actor, "updated_settings", "HostelSetting", current.Id.ToString());
        await db.SaveChangesAsync();
    }

    private async Task RecalculateRoomOccupancyAsync(Guid roomId)
    {
        var room = await db.Rooms.Include(room => room.Beds).FirstAsync(room => room.Id == roomId);
        room.TotalBeds = Math.Max(room.TotalBeds, room.Beds.Count);
        room.OccupiedBeds = room.Beds.Count(bed => bed.Status == "occupied");
        room.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task EnsureResidentCategoryAsync(string gender)
    {
        var category = await db.HostelSettings.AsNoTracking()
            .OrderBy(setting => setting.Id)
            .Select(setting => setting.ResidentCategory)
            .FirstAsync();
        if ((category == "men" && gender != "male") || (category == "women" && gender != "female"))
        {
            throw new InvalidOperationException($"This property accepts {category} residents only.");
        }
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task ApplyLateFeesAsync(string actor)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var settings = await db.HostelSettings.AsNoTracking().OrderBy(setting => setting.Id).FirstAsync();
        var overdueBills = await db.Bills
            .Where(bill =>
                bill.DueDate < today &&
                bill.Status != "paid" &&
                bill.Status != "cancelled")
            .ToListAsync();

        if (overdueBills.Count == 0)
        {
            return;
        }

        var changed = 0;
        foreach (var bill in overdueBills)
        {
            var originalStatus = bill.Status;
            var originalLateFee = bill.LateFeeAmount;

            if (bill.LateFeeAmount <= 0 && settings.LateFee > 0)
            {
                bill.LateFeeAmount = settings.LateFee;
            }

            bill.Status = bill.PaidAmount > 0 ? "partially_paid" : "overdue";

            if (bill.Status != originalStatus || bill.LateFeeAmount != originalLateFee)
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            await AddAuditAsync(actor, "applied_late_fees", "Bill", changed.ToString());
            await db.SaveChangesAsync();
        }
    }

    private async Task UpdateTenantKycStatusAsync(Guid tenantId)
    {
        var tenant = await db.Tenants.FirstAsync(tenant => tenant.Id == tenantId);
        var documents = await db.TenantDocuments.Where(document => document.TenantId == tenantId).ToListAsync();

        tenant.KycStatus = documents.Count == 0
            ? "pending"
            : documents.Any(document => document.Status == "rejected")
                ? "rejected"
                : documents.All(document => document.Status == "approved")
                    ? "approved"
                    : "in_review";
    }

    private async Task RecalculateMessCountsAsync(DateOnly mealDate, string mealType)
    {
        var menu = await db.MessMenus.FirstOrDefaultAsync(menu => menu.MenuDate == mealDate && menu.MealType == mealType);
        if (menu is null)
        {
            return;
        }

        var counts = await db.MessAttendances
            .Where(attendance => attendance.MealDate == mealDate && attendance.MealType == mealType)
            .GroupBy(attendance => attendance.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync();

        menu.OptInCount = counts.FirstOrDefault(group => group.Status == "opt_in")?.Count ?? 0;
        menu.OptOutCount = counts.FirstOrDefault(group => group.Status == "opt_out")?.Count ?? 0;
    }

    private static string CreateInvoiceNumber() => $"INV{DateTime.UtcNow:yyyyMMddHHmmssfff}";

    private static Bill CreateSecurityDepositBill(Guid tenantId, decimal amount) => new()
    {
        TenantId = tenantId,
        BillMonth = "Security Deposit",
        BillYear = DateTime.Today.Year,
        RentAmount = amount,
        DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
        InvoiceNumber = $"DEP{DateTime.UtcNow:yyyyMMddHHmmssfff}",
        Status = "pending"
    };

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 7 || value[0] != '#' || !value[1..].All(Uri.IsHexDigit))
        {
            return fallback;
        }

        return value.ToLowerInvariant();
    }

    private string NormalizeImageUrl(string? value)
    {
        var imageUrl = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        if (imageUrl.StartsWith("/images/", StringComparison.Ordinal)
            && !imageUrl.Contains("..", StringComparison.Ordinal)
            && !imageUrl.Contains('\\'))
        {
            return imageUrl;
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("Image URLs must use HTTPS or a locally uploaded /images/ path.");
        }

        var allowedHosts = (configuration["Security:AllowedImageHosts"] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!allowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Image host '{uri.Host}' is not in Security:AllowedImageHosts.");
        }

        return uri.AbsoluteUri;
    }

    private async Task AddAuditAsync(string actor, string action, string entity, string entityId)
    {
        await db.AuditLogs.AddAsync(new AuditLog
        {
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor,
            Action = action,
            Entity = entity,
            EntityId = entityId
        });
    }
}
