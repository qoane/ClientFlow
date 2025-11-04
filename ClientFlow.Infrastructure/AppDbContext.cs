using ClientFlow.Domain.Surveys;
using Microsoft.EntityFrameworkCore;

namespace ClientFlow.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveySection> Sections => Set<SurveySection>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<Answer> Answers => Set<Answer>();

    public DbSet<QuestionOption> Options => Set<QuestionOption>();
    public DbSet<QuestionRule> Rules => Set<QuestionRule>();
    public DbSet<ResponseSession> Sessions => Set<ResponseSession>();

    // Kiosk feedback tables
    public DbSet<ClientFlow.Domain.Feedback.Staff> Staff => Set<ClientFlow.Domain.Feedback.Staff>();
    public DbSet<ClientFlow.Domain.Feedback.KioskFeedback> KioskFeedback => Set<ClientFlow.Domain.Feedback.KioskFeedback>();

    // Branch and user tables
    public DbSet<ClientFlow.Domain.Branches.Branch> Branches => Set<ClientFlow.Domain.Branches.Branch>();
    public DbSet<ClientFlow.Domain.Users.User> Users => Set<ClientFlow.Domain.Users.User>();

    // Settings key/value storage
    public DbSet<ClientFlow.Domain.Settings.Setting> Settings => Set<ClientFlow.Domain.Settings.Setting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---- Settings mapping ----
        b.Entity<ClientFlow.Domain.Settings.Setting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Id).ValueGeneratedOnAdd();      // maps to NEWID() in SQL
            e.Property(x => x.Key).HasMaxLength(256).IsRequired();
        });

        // ---- Answers & relationships ----
        b.Entity<Answer>()
            .HasOne(a => a.Response)
            .WithMany(r => r.Answers)
            .HasForeignKey(a => a.ResponseId);

        b.Entity<Answer>()
            .HasOne(a => a.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.QuestionId);

        // Single, consistent precision
        b.Entity<Answer>()
            .Property(a => a.ValueNumber)
            .HasPrecision(10, 2);

        // ---- Seed Liberty survey ----
        var surveyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sectionClientId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var sectionServiceId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbc");
        var sectionCoreId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbd");
        var sectionFeedbackId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbe");
        var sectionEndId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbf");

        var qPhoneId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var qStaffId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var qServiceId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var qSatisfactionId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var qTimelinessId = Guid.Parse("10000000-0000-0000-0000-000000000005");
        var qProfessionalismId = Guid.Parse("10000000-0000-0000-0000-000000000006");
        var qRecommendId = Guid.Parse("10000000-0000-0000-0000-000000000007");
        var qFollowUpId = Guid.Parse("10000000-0000-0000-0000-000000000008");
        var qImproveId = Guid.Parse("10000000-0000-0000-0000-000000000009");
        var qEndMessageId = Guid.Parse("10000000-0000-0000-0000-00000000000A");

        b.Entity<Survey>().HasData(new Survey
        {
            Id = surveyId,
            Code = "liberty-nps",
            Title = "Liberty NPS 2025",
            Description = "How likely are you to recommend Liberty?",
            IsActive = true,
            ThemeJson = """{"accent":"#de2b2b"}"""
        });

        b.Entity<SurveySection>().HasData(
            new SurveySection
            {
                Id = sectionClientId,
                SurveyId = surveyId,
                Title = "Client Identification",
                Order = 1,
                Columns = 1
            },
            new SurveySection
            {
                Id = sectionServiceId,
                SurveyId = surveyId,
                Title = "Service Interaction",
                Order = 2,
                Columns = 1
            },
            new SurveySection
            {
                Id = sectionCoreId,
                SurveyId = surveyId,
                Title = "Core Satisfaction Questions",
                Order = 3,
                Columns = 1
            },
            new SurveySection
            {
                Id = sectionFeedbackId,
                SurveyId = surveyId,
                Title = "Feedback & Follow-Up",
                Order = 4,
                Columns = 1
            },
            new SurveySection
            {
                Id = sectionEndId,
                SurveyId = surveyId,
                Title = "End Screen",
                Order = 5,
                Columns = 1
            }
        );

        b.Entity<Question>().HasData(
            new Question
            {
                Id = qPhoneId,
                SurveyId = surveyId,
                SectionId = sectionClientId,
                Order = 1,
                Type = "phone",
                Prompt = "Please enter your phone number.",
                Key = "phone",
                Required = true
            },
            new Question
            {
                Id = qStaffId,
                SurveyId = surveyId,
                SectionId = sectionServiceId,
                Order = 2,
                Type = "radio",
                Prompt = "I was assisted by…",
                Key = "staff",
                Required = true
            },
            new Question
            {
                Id = qServiceId,
                SurveyId = surveyId,
                SectionId = sectionServiceId,
                Order = 3,
                Type = "radio",
                Prompt = "Which Liberty service were you assisted with today?",
                Key = "service",
                Required = true
            },
            new Question
            {
                Id = qSatisfactionId,
                SurveyId = surveyId,
                SectionId = sectionCoreId,
                Order = 4,
                Type = "radio",
                Prompt = "How satisfied are you with your recent interaction with Liberty?",
                Key = "satisfaction",
                Required = true
            },
            new Question
            {
                Id = qTimelinessId,
                SurveyId = surveyId,
                SectionId = sectionCoreId,
                Order = 5,
                Type = "radio",
                Prompt = "Was your query handled within a reasonable time?",
                Key = "timeliness",
                Required = true
            },
            new Question
            {
                Id = qProfessionalismId,
                SurveyId = surveyId,
                SectionId = sectionCoreId,
                Order = 6,
                Type = "radio",
                Prompt = "Did our staff treat you professionally and with respect?",
                Key = "professionalism",
                Required = true
            },
            new Question
            {
                Id = qRecommendId,
                SurveyId = surveyId,
                SectionId = sectionCoreId,
                Order = 7,
                Type = "radio",
                Prompt = "How likely are you to recommend Liberty to a friend or family member?",
                Key = "recommend",
                Required = true
            },
            new Question
            {
                Id = qFollowUpId,
                SurveyId = surveyId,
                SectionId = sectionFeedbackId,
                Order = 8,
                Type = "radio",
                Prompt = "Would you like someone to contact you about your experience?",
                Key = "follow_up",
                Required = true
            },
            new Question
            {
                Id = qImproveId,
                SurveyId = surveyId,
                SectionId = sectionFeedbackId,
                Order = 9,
                Type = "text",
                Prompt = "Please tell us briefly what we could do better.",
                Key = "improvement",
                Required = false,
                SettingsJson = """{"placeholder":"Your feedback helps us improve."}"""
            },
            new Question
            {
                Id = qEndMessageId,
                SurveyId = surveyId,
                SectionId = sectionEndId,
                Order = 10,
                Type = "message",
                Prompt = "Thank you for your feedback! Your input helps us improve our service.",
                Key = "end",
                Required = false
            }
        );

        b.Entity<QuestionOption>().HasData(
            // Staff list
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), QuestionId = qStaffId, Value = "neo-ramohabi", Label = "Neo Ramohabi", Order = 1 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), QuestionId = qStaffId, Value = "baradi-boikanyo", Label = "Baradi Boikanyo", Order = 2 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), QuestionId = qStaffId, Value = "tsepo-chefa", Label = "Tsepo Chefa", Order = 3 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), QuestionId = qStaffId, Value = "mpho-phalafang", Label = "Mpho Phalafang", Order = 4 },

            // Service type
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000101"), QuestionId = qServiceId, Value = "policy-enquiry", Label = "Policy enquiry", Order = 1 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000102"), QuestionId = qServiceId, Value = "claim", Label = "Claim", Order = 2 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000103"), QuestionId = qServiceId, Value = "new-policy-application", Label = "New policy application", Order = 3 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000104"), QuestionId = qServiceId, Value = "payment", Label = "Payment", Order = 4 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000105"), QuestionId = qServiceId, Value = "amendments", Label = "Amendments/ Policy Changes", Order = 5 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000106"), QuestionId = qServiceId, Value = "other", Label = "Other", Order = 6 },

            // Satisfaction scale
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000201"), QuestionId = qSatisfactionId, Value = "1", Label = "😊 1 = Excellent", Order = 1 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000202"), QuestionId = qSatisfactionId, Value = "2", Label = "😃 2 = Good", Order = 2 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000203"), QuestionId = qSatisfactionId, Value = "3", Label = "😐 3 = Okay", Order = 3 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000204"), QuestionId = qSatisfactionId, Value = "4", Label = "😞 4 = Poor", Order = 4 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000205"), QuestionId = qSatisfactionId, Value = "5", Label = "😡 5 = Very Poor", Order = 5 },

            // Timeliness scale
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000301"), QuestionId = qTimelinessId, Value = "not-at-all", Label = "Not at all", Order = 1 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000302"), QuestionId = qTimelinessId, Value = "no", Label = "No", Order = 2 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000303"), QuestionId = qTimelinessId, Value = "neutral", Label = "Neutral", Order = 3 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000304"), QuestionId = qTimelinessId, Value = "yes-mostly", Label = "Yes, mostly", Order = 4 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000305"), QuestionId = qTimelinessId, Value = "absolutely", Label = "Absolutely", Order = 5 },

            // Professionalism scale (same labels)
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000401"), QuestionId = qProfessionalismId, Value = "not-at-all", Label = "Not at all", Order = 1 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000402"), QuestionId = qProfessionalismId, Value = "no", Label = "No", Order = 2 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000403"), QuestionId = qProfessionalismId, Value = "neutral", Label = "Neutral", Order = 3 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000404"), QuestionId = qProfessionalismId, Value = "yes-mostly", Label = "Yes, mostly", Order = 4 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000405"), QuestionId = qProfessionalismId, Value = "absolutely", Label = "Absolutely", Order = 5 },

            // Recommendation scale
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000501"), QuestionId = qRecommendId, Value = "1", Label = "😊 1 = Extremely likely", Order = 1 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000502"), QuestionId = qRecommendId, Value = "2", Label = "😃 2 = Very likely", Order = 2 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000503"), QuestionId = qRecommendId, Value = "3", Label = "😐 3 = Neutral", Order = 3 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000504"), QuestionId = qRecommendId, Value = "4", Label = "😞 4 = Slightly likely", Order = 4 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000505"), QuestionId = qRecommendId, Value = "5", Label = "😡 5 = Not at all likely", Order = 5 },

            // Follow-up yes/no
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000601"), QuestionId = qFollowUpId, Value = "yes", Label = "Yes", Order = 1 },
            new QuestionOption { Id = Guid.Parse("20000000-0000-0000-0000-000000000602"), QuestionId = qFollowUpId, Value = "no", Label = "No", Order = 2 }
        );

        b.Entity<QuestionOption>()
            .HasIndex(x => new { x.QuestionId, x.Order })
            .IsUnique();

        // ---- Seed sample staff ----
        // Staff entries are seeded with a default branch assignment.  When adding additional branches you can
        // update the BranchId column via subsequent migrations or within the application itself.  The IDs for
        // staff members are deterministic so that additional data (e.g. BranchId) can be updated without
        // re‑seeding duplicate rows.  Each staff record is assigned to the default Head Office branch defined
        // below.  PhotoUrl is left null because pictures are uploaded at runtime.
        // Prepare deterministic GUIDs for the initial staff and the default branch.  The defaultBranchId
        // constant is reused in both the branch seeding and the staff seeding to ensure they are linked.
        var staff1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var staff2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var staff3Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var staff4Id = Guid.Parse("44444444-4444-4444-4444-444444444444");
        // Define the default branch ID early so it can be referenced from the staff seeding.  This ID will
        // also be used when seeding the Branch entity below.
        var defaultBranchId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        b.Entity<ClientFlow.Domain.Feedback.Staff>().HasData(
            new ClientFlow.Domain.Feedback.Staff { Id = staff1Id, Name = "Neo Ramohabi", PhotoUrl = null, IsActive = true, BranchId = defaultBranchId },
            new ClientFlow.Domain.Feedback.Staff { Id = staff2Id, Name = "Baradi Boikanyo", PhotoUrl = null, IsActive = true, BranchId = defaultBranchId },
            new ClientFlow.Domain.Feedback.Staff { Id = staff3Id, Name = "Tsepo Chefa", PhotoUrl = null, IsActive = true, BranchId = defaultBranchId },
            new ClientFlow.Domain.Feedback.Staff { Id = staff4Id, Name = "Mpho Phalafang", PhotoUrl = null, IsActive = true, BranchId = defaultBranchId }
        );

        // ---- KioskFeedback relationship ----
        b.Entity<ClientFlow.Domain.Feedback.KioskFeedback>()
            .HasOne(k => k.Staff)
            .WithMany(s => s.Feedback)
            .HasForeignKey(k => k.StaffId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---- Branch & User mapping ----
        // Branches have unique names and optional report configuration
        b.Entity<ClientFlow.Domain.Branches.Branch>(e =>
        {
            e.ToTable("Branches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Users have unique email addresses and optional branch assignment
        b.Entity<ClientFlow.Domain.Users.User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).IsRequired();
            // BranchId is optional for Admin/SuperAdmin
            e.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Staff belong to a branch (nullable); when branch is deleted the staff remain with null BranchId
        b.Entity<ClientFlow.Domain.Feedback.Staff>()
            .HasOne(s => s.Branch)
            .WithMany()
            .HasForeignKey(s => s.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        // Kiosk feedback belongs to a branch; deleting a branch will restrict removal if feedback exists
        b.Entity<ClientFlow.Domain.Feedback.KioskFeedback>()
            .HasOne(k => k.Branch)
            .WithMany()
            .HasForeignKey(k => k.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Seeding default branches ----
        // Use the defaultBranchId defined earlier when seeding the Branch entity.  This ensures the branch
        // referenced by seeded staff matches the branch record.
        b.Entity<ClientFlow.Domain.Branches.Branch>().HasData(new ClientFlow.Domain.Branches.Branch
        {
            Id = defaultBranchId,
            Name = "Head Office",
            ReportRecipients = "",
            ReportTime = "08:00"
        });

        // ---- Seed an initial SuperAdmin user ----
        // NOTE: PasswordHash here is a SHA256 hash of "Admin123!" encoded in Base64.
        b.Entity<ClientFlow.Domain.Users.User>().HasData(new ClientFlow.Domain.Users.User
        {
            Id = Guid.Parse("11111111-0000-0000-0000-000000000000"),
            Email = "admin@example.com",
            PasswordHash = "PrP+ZrMeO00Q+nC1ytSccRIpSvauTkdqHEBRVdRaoSE=",
            Role = ClientFlow.Domain.Users.UserRole.SuperAdmin,
            BranchId = null
        });

        // ---- Update sample staff seeding to assign them to the default branch ----
        // The above HasData call already assigns the BranchId for each seeded staff member.  If additional
        // properties need to be updated for the same seeded IDs, you can add another HasData call with
        // matching IDs and the new property values.  Here we leave this section empty to avoid duplicating
        // the staff seeding.

        // ---- Settings seeding (use NON-EMPTY GUIDs) ----
        b.Entity<ClientFlow.Domain.Settings.Setting>().HasData(
            new ClientFlow.Domain.Settings.Setting
            {
                Id = Guid.Parse("40EAF4A3-5D07-4A5B-9C8D-6A2E6E3DC0F1"),
                Key = "ReportRecipients",
                Value = ""
            },
            new ClientFlow.Domain.Settings.Setting
            {
                Id = Guid.Parse("6B7D2F8B-0F6F-4A5E-9F7C-0E8E2D2C9A11"),
                Key = "BranchName",
                Value = "Head Office"
            },
            new ClientFlow.Domain.Settings.Setting
            {
                Id = Guid.Parse("E2C4B8E9-1A3D-4E8F-9C0B-3F2E6D7A9B55"),
                Key = "ReportTime",
                Value = "08:00"
            }
        );
    }
}
