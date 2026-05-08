using Microsoft.AspNetCore.Identity;
using SmartEventManagement.Models;

namespace SmartEventManagement.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        // Ensure roles exist
        string[] roles = { "Admin", "Organiser", "Attendee" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Admin
        var admin = await EnsureUser(userManager, "admin@koi.edu.au", "Admin@123", "Administrator", "Admin", true, "");
        await EnsureRole(userManager, admin, "Admin");

        // Organisers
        var org1 = await EnsureUser(userManager, "organiser1@koi.edu.au", "Organiser@123", "Alice Organiser", "Organiser", true, "Music,Tech,Business");
        await EnsureRole(userManager, org1, "Organiser");

        var org2 = await EnsureUser(userManager, "organiser2@koi.edu.au", "Organiser@123", "Bob Organiser", "Organiser", true, "Arts,Sports,Education");
        await EnsureRole(userManager, org2, "Organiser");

        // Attendees
        var att1 = await EnsureUser(userManager, "attendee1@koi.edu.au", "Attendee@123", "Charlie Attendee", "Attendee", true, "Tech,Music");
        await EnsureRole(userManager, att1, "Attendee");

        var att2 = await EnsureUser(userManager, "attendee2@koi.edu.au", "Attendee@123", "Diana Attendee", "Attendee", true, "Arts,Business");
        await EnsureRole(userManager, att2, "Attendee");

        var att3 = await EnsureUser(userManager, "attendee3@koi.edu.au", "Attendee@123", "Ethan Attendee", "Attendee", true, "Sports,Education");
        await EnsureRole(userManager, att3, "Attendee");

        // Seed events if none exist
        if (!db.Events.Any())
        {
            var events = new List<Event>
            {
                new Event
                {
                    Title = "Tech Innovation Summit 2025",
                    Description = "Join industry leaders to explore the latest in AI, cloud computing, and digital transformation. Network with top professionals and gain insights into the future of technology.",
                    Location = "Sydney Convention Centre, Sydney NSW",
                    Category = "Tech",
                    Date = DateTime.Today.AddDays(15),
                    Time = new TimeSpan(9, 0, 0),
                    Capacity = 200,
                    Status = "Approved",
                    OrganiserId = org1.Id
                },
                new Event
                {
                    Title = "Business Leadership Forum",
                    Description = "An exclusive forum for business leaders featuring keynote speeches, panel discussions, and workshops on strategic leadership and organisational growth.",
                    Location = "Melbourne Business Hub, Melbourne VIC",
                    Category = "Business",
                    Date = DateTime.Today.AddDays(22),
                    Time = new TimeSpan(10, 0, 0),
                    Capacity = 150,
                    Status = "Approved",
                    OrganiserId = org1.Id
                },
                new Event
                {
                    Title = "Live Music Night: Jazz & Blues",
                    Description = "An unforgettable evening of live jazz and blues performances featuring local and international artists. Food and beverages included.",
                    Location = "The Jazz Lounge, Brisbane QLD",
                    Category = "Music",
                    Date = DateTime.Today.AddDays(10),
                    Time = new TimeSpan(19, 0, 0),
                    Capacity = 100,
                    Status = "Approved",
                    OrganiserId = org1.Id
                },
                new Event
                {
                    Title = "Digital Marketing Masterclass",
                    Description = "A comprehensive full-day workshop covering SEO, social media strategy, content marketing, and paid advertising for modern businesses.",
                    Location = "Perth Learning Centre, Perth WA",
                    Category = "Education",
                    Date = DateTime.Today.AddDays(30),
                    Time = new TimeSpan(8, 30, 0),
                    Capacity = 75,
                    Status = "Pending",
                    OrganiserId = org1.Id
                },
                new Event
                {
                    Title = "Community Sports Day",
                    Description = "A fun-filled community sports day featuring cricket, soccer, basketball, and athletics. Open to all ages and fitness levels. Prizes for winners!",
                    Location = "Adelaide Oval, Adelaide SA",
                    Category = "Sports",
                    Date = DateTime.Today.AddDays(45),
                    Time = new TimeSpan(8, 0, 0),
                    Capacity = 300,
                    Status = "Pending",
                    OrganiserId = org1.Id
                }
            };

            db.Events.AddRange(events);
            await db.SaveChangesAsync();

            // Add some registrations for approved events
            var approvedEvents = db.Events.Where(e => e.Status == "Approved").ToList();
            if (approvedEvents.Any())
            {
                foreach (var ev in approvedEvents)
                {
                    db.Registrations.Add(new Registration
                    {
                        EventId = ev.Id,
                        AttendeeId = att1.Id,
                        RegisteredAt = DateTime.UtcNow.AddDays(-2),
                        Cancelled = false
                    });
                }
                db.Registrations.Add(new Registration
                {
                    EventId = approvedEvents[0].Id,
                    AttendeeId = att2.Id,
                    RegisteredAt = DateTime.UtcNow.AddDays(-1),
                    Cancelled = false
                });
                await db.SaveChangesAsync();

                // Add feedback for past events
                db.Feedbacks.Add(new Feedback
                {
                    EventId = approvedEvents[0].Id,
                    AttendeeId = att1.Id,
                    Rating = 5,
                    Comment = "Absolutely amazing event! Well organised and very informative.",
                    SubmittedAt = DateTime.UtcNow.AddDays(-1)
                });
                await db.SaveChangesAsync();

                // Add a welcome notification for each attendee
                var attendees = new[] { att1, att2, att3 };
                foreach (var att in attendees)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = att.Id,
                        Message = "Welcome to Smart Event Management! Browse and register for exciting events.",
                        Type = "info",
                        SentAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUser(
        UserManager<ApplicationUser> userManager,
        string email, string password, string fullName, string role, bool isApproved, string interests)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Role = role,
                IsApproved = isApproved,
                RegisteredAt = DateTime.UtcNow,
                Interests = interests,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new Exception($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        return user;
    }

    private static async Task EnsureRole(UserManager<ApplicationUser> userManager, ApplicationUser user, string role)
    {
        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
    }
}
