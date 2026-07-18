using HostelPro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HostelPro.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var seedDemoData = environment.IsDevelopment() && configuration.GetValue("SeedDemoData", false);

        foreach (var role in new[] { AppRoles.Admin, AppRoles.User })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Hostel Administrator",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(admin, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, AppRoles.Admin);
                }
            }
        }

        if (!await db.HostelSettings.AnyAsync())
        {
            db.HostelSettings.Add(new HostelSetting
            {
                HostelName = "Your PG",
                PropertyModel = "pg",
                ResidentCategory = "mixed",
                Tagline = "Comfortable stays, managed professionally",
                HeroTitle = "A better place",
                HeroHighlight = "to call home",
                HeroDescription = "Safe, comfortable accommodation with clear pricing and responsive management.",
                HeroImageUrl = "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=1600&q=85",
                PrimaryColor = "#263f95",
                AccentColor = "#e57942",
                AboutDescription = "We provide well-managed accommodation focused on comfort, safety, and a dependable resident experience.",
                FooterDescription = "Comfortable accommodation with professional management and resident-first service.",
                FoundedYear = DateTime.Today.Year,
                ContactEmail = configuration["Email:AdminAddress"] ?? string.Empty,
                UpiPayeeName = configuration["Payment:UpiPayeeName"] ?? "HostelPro",
                UpiId = configuration["Payment:UpiId"] ?? string.Empty,
                PaymentTestMode = configuration.GetValue("Payment:TestMode", true),
                BillingDay = 1,
                DueDay = 5,
                LateFee = 100,
                HostelRules = "Visitors allowed only during 10 AM - 8 PM.\nMaintain silence after 10 PM.\nSmoking and alcohol strictly prohibited.",
                PaymentGatewayProvider = "manual",
                PaymentGatewayEnabled = false,
                PublicRegistrationEnabled = true
            });
        }
        else
        {
            var setting = await db.HostelSettings.OrderBy(item => item.Id).FirstAsync();
            setting.Tagline = string.IsNullOrWhiteSpace(setting.Tagline) ? "Comfortable stays, managed professionally" : setting.Tagline;
            setting.PropertyModel = setting.PropertyModel == "co_living" ? "co_living" : "pg";
            setting.ResidentCategory = setting.ResidentCategory is "men" or "women" ? setting.ResidentCategory : "mixed";
            setting.HeroTitle = string.IsNullOrWhiteSpace(setting.HeroTitle) ? "A better place" : setting.HeroTitle;
            setting.HeroHighlight = string.IsNullOrWhiteSpace(setting.HeroHighlight) ? "to call home" : setting.HeroHighlight;
            setting.HeroDescription = string.IsNullOrWhiteSpace(setting.HeroDescription)
                ? "Safe, comfortable accommodation with clear pricing and responsive management."
                : setting.HeroDescription;
            setting.HeroImageUrl = string.IsNullOrWhiteSpace(setting.HeroImageUrl)
                ? "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=1600&q=85"
                : setting.HeroImageUrl;
            setting.PrimaryColor = string.IsNullOrWhiteSpace(setting.PrimaryColor) ? "#263f95" : setting.PrimaryColor;
            setting.AccentColor = string.IsNullOrWhiteSpace(setting.AccentColor) ? "#e57942" : setting.AccentColor;
            setting.AboutDescription = string.IsNullOrWhiteSpace(setting.AboutDescription)
                ? "We provide well-managed accommodation focused on comfort, safety, and a dependable resident experience."
                : setting.AboutDescription;
            setting.FooterDescription = string.IsNullOrWhiteSpace(setting.FooterDescription)
                ? setting.AboutDescription
                : setting.FooterDescription;
            setting.FoundedYear = setting.FoundedYear is < 1900 or > 2100 ? DateTime.Today.Year : setting.FoundedYear;
            setting.WhatsAppPhone = string.IsNullOrWhiteSpace(setting.WhatsAppPhone) ? setting.ContactPhone : setting.WhatsAppPhone;
            setting.BillingDay = setting.BillingDay <= 0 ? 1 : setting.BillingDay;
            setting.DueDay = setting.DueDay <= 0 ? 5 : setting.DueDay;
            setting.LateFee = setting.LateFee <= 0 ? 100 : setting.LateFee;
            setting.HostelRules = string.IsNullOrWhiteSpace(setting.HostelRules)
                ? "Visitors allowed only during 10 AM - 8 PM.\nMaintain silence after 10 PM.\nSmoking and alcohol strictly prohibited."
                : setting.HostelRules;
            setting.PaymentGatewayProvider = string.IsNullOrWhiteSpace(setting.PaymentGatewayProvider)
                ? "manual"
                : setting.PaymentGatewayProvider;
        }

        if (seedDemoData && !await db.Expenses.AnyAsync())
        {
            db.Expenses.AddRange(
                new Expense
                {
                    Category = "utilities",
                    Title = "Electricity and water",
                    Amount = 12500,
                    ExpenseDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
                    Notes = "Monthly utilities estimate"
                },
                new Expense
                {
                    Category = "internet",
                    Title = "Broadband connection",
                    Amount = 2500,
                    ExpenseDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-3)),
                    Notes = "High-speed Wi-Fi plan"
                });
        }

        if (seedDemoData && !await db.MessMenus.AnyAsync())
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            db.MessMenus.AddRange(
                new MessMenu
                {
                    MenuDate = today,
                    MealType = "breakfast",
                    MenuItems = "Idli, sambar, chutney, tea"
                },
                new MessMenu
                {
                    MenuDate = today,
                    MealType = "dinner",
                    MenuItems = "Rice, dal, mixed vegetable curry, curd"
                },
                new MessMenu
                {
                    MenuDate = today.AddDays(1),
                    MealType = "lunch",
                    MenuItems = "Chapati, paneer curry, rice, salad"
                });
        }

        if (seedDemoData && !await db.Rooms.AnyAsync())
        {
            var room101 = new Room
            {
                RoomNumber = "101",
                FloorNumber = "1",
                RoomType = "single",
                TotalBeds = 1,
                OccupiedBeds = 1,
                MonthlyRent = 18000,
                Status = "active",
                Amenities = "AC, Attached Bath, Study Desk, Wardrobe",
                Description = "Premium single occupancy room with AC and attached bath.",
                CoverImageUrl = "https://images.unsplash.com/photo-1595526114035-0d45ed16cfbf?auto=format&fit=crop&w=1200&q=80"
            };
            var room203 = new Room
            {
                RoomNumber = "203",
                FloorNumber = "2",
                RoomType = "double",
                TotalBeds = 2,
                OccupiedBeds = 1,
                MonthlyRent = 12500,
                Status = "active",
                Amenities = "Wi-Fi, Study Desk, Balcony, Power Backup",
                Description = "Bright two-sharing room with quiet study corners.",
                CoverImageUrl = "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=1200&q=80"
            };
            var room305 = new Room
            {
                RoomNumber = "305",
                FloorNumber = "3",
                RoomType = "triple",
                TotalBeds = 3,
                OccupiedBeds = 0,
                MonthlyRent = 8500,
                Status = "active",
                Amenities = "Locker, Ceiling Fan, Laundry Access",
                Description = "Budget-friendly triple-sharing room near the common study hall.",
                CoverImageUrl = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=1200&q=80"
            };

            room101.Beds.Add(new Bed { BedNumber = "A", Status = "occupied" });
            room203.Beds.Add(new Bed { BedNumber = "A", Status = "occupied" });
            room203.Beds.Add(new Bed { BedNumber = "B", Status = "available" });
            room305.Beds.Add(new Bed { BedNumber = "A", Status = "available" });
            room305.Beds.Add(new Bed { BedNumber = "B", Status = "available" });
            room305.Beds.Add(new Bed { BedNumber = "C", Status = "available" });

            db.Rooms.AddRange(room101, room203, room305);
        }

        if (seedDemoData && !await db.GalleryImages.AnyAsync())
        {
            db.GalleryImages.AddRange(
                new GalleryImage
                {
                    Album = "Rooms",
                    Caption = "Clean furnished rooms",
                    ImageUrl = "https://images.unsplash.com/photo-1555854877-bab0e564b8d5?auto=format&fit=crop&w=1200&q=80",
                    SortOrder = 1
                },
                new GalleryImage
                {
                    Album = "Common Area",
                    Caption = "Study lounge and community space",
                    ImageUrl = "https://images.unsplash.com/photo-1524758631624-e2822e304c36?auto=format&fit=crop&w=1200&q=80",
                    SortOrder = 2
                },
                new GalleryImage
                {
                    Album = "Dining",
                    Caption = "Managed dining area",
                    ImageUrl = "https://images.unsplash.com/photo-1555396273-367ea4eb4db5?auto=format&fit=crop&w=1200&q=80",
                    SortOrder = 3
                });
        }

        if (!await db.Amenities.AnyAsync())
        {
            db.Amenities.AddRange(
                new Amenity { Name = "24/7 Security", Description = "Monitored premises and responsive on-site support", IconKey = "shield", SortOrder = 1 },
                new Amenity { Name = "High-Speed Wi-Fi", Description = "Reliable internet access in rooms and common areas", IconKey = "wifi", SortOrder = 2 },
                new Amenity { Name = "Homely Food", Description = "Fresh meal options managed through the resident portal", IconKey = "food", SortOrder = 3 },
                new Amenity { Name = "Hot Water", Description = "Dependable hot water access for residents", IconKey = "water", SortOrder = 4 },
                new Amenity { Name = "Power Backup", Description = "Backup power for essential services and common areas", IconKey = "power", SortOrder = 5 },
                new Amenity { Name = "Parking", Description = "Organized parking for two-wheelers and bicycles", IconKey = "parking", SortOrder = 6 },
                new Amenity { Name = "Laundry", Description = "Convenient washing and ironing service options", IconKey = "laundry", SortOrder = 7 },
                new Amenity { Name = "Housekeeping", Description = "Regular cleaning and maintenance support", IconKey = "housekeeping", SortOrder = 8 });
        }

        if (seedDemoData && !await db.Testimonials.AnyAsync())
        {
            db.Testimonials.AddRange(
                new Testimonial { Name = "Ravi Kumar", Role = "Resident", Rating = 5, Quote = "Clean rooms, responsive management, and a comfortable stay.", SortOrder = 1 },
                new Testimonial { Name = "Priya Sharma", Role = "Student", Rating = 5, Quote = "The Wi-Fi is reliable and maintenance requests are handled quickly.", SortOrder = 2 },
                new Testimonial { Name = "Amit Patel", Role = "Working professional", Rating = 4, Quote = "Clear billing and timely communication make the experience easy.", SortOrder = 3 });
        }

        var roomsWithLocalImages = await db.Rooms
            .Where(room => room.CoverImageUrl.StartsWith("/images/"))
            .ToListAsync();
        foreach (var room in roomsWithLocalImages.Where(room => !LocalImageExists(environment.WebRootPath, room.CoverImageUrl)))
        {
            room.CoverImageUrl = string.Empty;
        }

        var galleryWithLocalImages = await db.GalleryImages
            .Where(image => image.ImageUrl.StartsWith("/images/"))
            .ToListAsync();
        foreach (var image in galleryWithLocalImages.Where(image => !LocalImageExists(environment.WebRootPath, image.ImageUrl)))
        {
            image.ImageUrl = string.Empty;
            image.IsPublished = false;
        }

        await db.SaveChangesAsync();
    }

    private static bool LocalImageExists(string webRootPath, string imageUrl)
    {
        var root = Path.GetFullPath(webRootPath) + Path.DirectorySeparatorChar;
        var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(webRootPath, relativePath));
        return fullPath.StartsWith(root, StringComparison.Ordinal) && File.Exists(fullPath);
    }
}
