namespace ElevateED.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Applicants",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        FirstName = c.String(nullable: false),
                        LastName = c.String(nullable: false),
                        DateOfBirth = c.DateTime(nullable: false),
                        Gender = c.Int(nullable: false),
                        IdentityNumber = c.String(nullable: false),
                        Nationality = c.String(),
                        HomeLanguage = c.String(),
                        CellPhone = c.String(nullable: false),
                        AlternativePhone = c.String(),
                        PhysicalAddress = c.String(nullable: false),
                        PostalAddress = c.String(),
                        PreviousSchool = c.String(nullable: false),
                        HighestGradePassed = c.String(nullable: false),
                        YearCompleted = c.Int(nullable: false),
                        PreviousSchoolAddress = c.String(),
                        AcademicAverage = c.Decimal(precision: 18, scale: 2),
                        GradeApplyingFor = c.String(nullable: false),
                        StreamChoice = c.String(),
                        ParentName = c.String(nullable: false),
                        ParentIdNumber = c.String(nullable: false),
                        ParentRelationship = c.String(nullable: false),
                        ParentCellPhone = c.String(nullable: false),
                        ParentEmail = c.String(nullable: false),
                        ParentWorkPhone = c.String(),
                        ParentOccupation = c.String(),
                        ParentEmployer = c.String(),
                        ParentWorkAddress = c.String(),
                        EmergencyContactName = c.String(nullable: false),
                        EmergencyContactPhone = c.String(nullable: false),
                        EmergencyContactRelationship = c.String(),
                        MedicalConditions = c.String(),
                        Allergies = c.String(),
                        CurrentMedication = c.String(),
                        DoctorName = c.String(),
                        DoctorPhone = c.String(),
                        MedicalAidName = c.String(),
                        MedicalAidNumber = c.String(),
                        IdDocumentPath = c.String(),
                        ReportCardPath = c.String(),
                        TransferCertificatePath = c.String(),
                        ProofOfResidencePath = c.String(),
                        ParentIdDocumentPath = c.String(),
                        Status = c.Int(nullable: false),
                        ApplicationDate = c.DateTime(nullable: false),
                        ReviewDate = c.DateTime(),
                        ReviewedBy = c.String(),
                        RejectionReason = c.String(),
                        AdminNotes = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ApplicationUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.ApplicationUsers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StudentNumber = c.String(nullable: false, maxLength: 20),
                        Email = c.String(nullable: false),
                        PasswordHash = c.String(nullable: false),
                        Role = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        LastLogin = c.DateTime(),
                        HasChangedPassword = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.PastPapers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false),
                        Subject = c.String(nullable: false),
                        Grade = c.String(nullable: false),
                        Year = c.String(nullable: false),
                        Term = c.String(nullable: false),
                        ExamType = c.String(nullable: false),
                        Description = c.String(),
                        FilePath = c.String(nullable: false),
                        MemoPath = c.String(),
                        UploadedBy = c.Int(nullable: false),
                        UploadedAt = c.DateTime(nullable: false),
                        IsPublished = c.Boolean(nullable: false),
                        DownloadCount = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.UploadedBy, cascadeDelete: true)
                .Index(t => t.UploadedBy);
            
            CreateTable(
                "dbo.Teachers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        FirstName = c.String(nullable: false),
                        LastName = c.String(nullable: false),
                        MiddleName = c.String(),
                        IdentityNumber = c.String(nullable: false),
                        DateOfBirth = c.DateTime(nullable: false),
                        PhoneNumber = c.String(nullable: false),
                        AlternativePhone = c.String(),
                        Address = c.String(),
                        Qualification = c.String(nullable: false),
                        YearsOfExperience = c.Int(nullable: false),
                        EmergencyContactName = c.String(),
                        EmergencyContactPhone = c.String(),
                        Subjects = c.String(),
                        Grades = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ApplicationUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Students",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        ApplicantId = c.Int(nullable: false),
                        FirstName = c.String(nullable: false, maxLength: 50),
                        MiddleName = c.String(maxLength: 50),
                        LastName = c.String(nullable: false, maxLength: 50),
                        DateOfBirth = c.DateTime(nullable: false),
                        Gender = c.Int(nullable: false),
                        IdentityNumber = c.String(nullable: false, maxLength: 20),
                        Nationality = c.String(maxLength: 50),
                        HomeLanguage = c.String(maxLength: 50),
                        CellPhone = c.String(nullable: false, maxLength: 15),
                        AlternativePhone = c.String(maxLength: 15),
                        PhysicalAddress = c.String(nullable: false),
                        PostalAddress = c.String(),
                        Grade = c.String(nullable: false),
                        ClassName = c.String(nullable: false),
                        StreamChoice = c.String(),
                        ParentName = c.String(nullable: false),
                        ParentIdNumber = c.String(nullable: false),
                        ParentRelationship = c.String(nullable: false),
                        ParentCellPhone = c.String(nullable: false),
                        ParentEmail = c.String(nullable: false),
                        ParentWorkPhone = c.String(),
                        ParentOccupation = c.String(),
                        ParentEmployer = c.String(),
                        ParentWorkAddress = c.String(),
                        EmergencyContactName = c.String(nullable: false),
                        EmergencyContactPhone = c.String(nullable: false),
                        EmergencyContactRelationship = c.String(),
                        MedicalConditions = c.String(),
                        Allergies = c.String(),
                        CurrentMedication = c.String(),
                        DoctorName = c.String(),
                        DoctorPhone = c.String(),
                        MedicalAidName = c.String(),
                        MedicalAidNumber = c.String(),
                        IdDocumentPath = c.String(),
                        ReportCardPath = c.String(),
                        TransferCertificatePath = c.String(),
                        ProofOfResidencePath = c.String(),
                        ParentIdDocumentPath = c.String(),
                        IsActive = c.Boolean(nullable: false),
                        EnrollmentDate = c.DateTime(nullable: false),
                        UpdatedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Applicants", t => t.ApplicantId)
                .ForeignKey("dbo.ApplicationUsers", t => t.UserId)
                .Index(t => t.UserId)
                .Index(t => t.ApplicantId);
            
            CreateTable(
                "dbo.StudyMaterials",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 500),
                        Subject = c.String(nullable: false, maxLength: 100),
                        GradeLevel = c.String(nullable: false, maxLength: 20),
                        FilePath = c.String(nullable: false),
                        FileName = c.String(nullable: false, maxLength: 200),
                        FileType = c.String(nullable: false, maxLength: 50),
                        FileSize = c.Long(nullable: false),
                        UploadedBy = c.Int(nullable: false),
                        UploadedDate = c.DateTime(nullable: false),
                        DownloadCount = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.UploadedBy, cascadeDelete: true)
                .Index(t => t.UploadedBy);
            
            CreateTable(
                "dbo.TeacherSubjectAssignments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TeacherId = c.Int(nullable: false),
                        Grade = c.String(nullable: false),
                        ClassName = c.String(nullable: false),
                        SubjectId = c.Int(nullable: false),
                        SubjectName = c.String(),
                        AssignedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Teachers", t => t.TeacherId, cascadeDelete: true)
                .Index(t => t.TeacherId);
            
            CreateTable(
                "dbo.TimeTables",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FilePath = c.String(nullable: false),
                        Type = c.String(nullable: false),
                        Grade = c.String(),
                        ClassName = c.String(),
                        Title = c.String(nullable: false),
                        Description = c.String(),
                        UploadedBy = c.Int(nullable: false),
                        UploadedAt = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.TeacherSubjectAssignments", "TeacherId", "dbo.Teachers");
            DropForeignKey("dbo.StudyMaterials", "UploadedBy", "dbo.Teachers");
            DropForeignKey("dbo.Students", "UserId", "dbo.ApplicationUsers");
            DropForeignKey("dbo.Students", "ApplicantId", "dbo.Applicants");
            DropForeignKey("dbo.PastPapers", "UploadedBy", "dbo.Teachers");
            DropForeignKey("dbo.Teachers", "UserId", "dbo.ApplicationUsers");
            DropForeignKey("dbo.Applicants", "UserId", "dbo.ApplicationUsers");
            DropIndex("dbo.TeacherSubjectAssignments", new[] { "TeacherId" });
            DropIndex("dbo.StudyMaterials", new[] { "UploadedBy" });
            DropIndex("dbo.Students", new[] { "ApplicantId" });
            DropIndex("dbo.Students", new[] { "UserId" });
            DropIndex("dbo.Teachers", new[] { "UserId" });
            DropIndex("dbo.PastPapers", new[] { "UploadedBy" });
            DropIndex("dbo.Applicants", new[] { "UserId" });
            DropTable("dbo.TimeTables");
            DropTable("dbo.TeacherSubjectAssignments");
            DropTable("dbo.StudyMaterials");
            DropTable("dbo.Students");
            DropTable("dbo.Teachers");
            DropTable("dbo.PastPapers");
            DropTable("dbo.ApplicationUsers");
            DropTable("dbo.Applicants");
        }
    }
}
