using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ElevateED.Models;

namespace ElevateED
{
    public class DatabaseConfig
    {
        public static void Initialize()
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<ElevateEDContext, Migrations.Configuration>());

            using (var context = new ElevateEDContext())
            {
                try
                {
                    // Seed Admin
                    if (!context.Users.Any(u => u.StudentNumber == "ADMIN001"))
                    {
                        var admin = new ApplicationUser
                        {
                            StudentNumber = "ADMIN001",
                            Email = "admin@mpiyakhehs.co.za",
                            PasswordHash = HashPassword("Admin@123"),  // NOW PROPERLY HASHED
                            Role = UserRole.Admin,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            HasChangedPassword = false
                        };
                        context.Users.Add(admin);
                        context.SaveChanges();
                        System.Diagnostics.Debug.WriteLine("Admin created: ADMIN001 / Admin@123");
                    }

                    // Seed Principal
                    if (!context.Users.Any(u => u.StudentNumber == "KHUZWAYO"))
                    {
                        var principal = new ApplicationUser
                        {
                            StudentNumber = "KHUZWAYO",
                            Email = "khuzwayo@mpiyakhehs.co.za",
                            PasswordHash = HashPassword("Hlupha123"),
                            Role = UserRole.Principal,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            HasChangedPassword = true
                        };
                        context.Users.Add(principal);
                        context.SaveChanges();
                        System.Diagnostics.Debug.WriteLine("Principal created: KHUZWAYO / Hlupha123");
                    }

                    // Seed Grades
                    if (!context.Grades.Any())
                    {
                        var grades = new List<Grade>
                        {
                            new Grade { Name = "Grade 8", Level = 8 },
                            new Grade { Name = "Grade 9", Level = 9 },
                            new Grade { Name = "Grade 10", Level = 10 },
                            new Grade { Name = "Grade 11", Level = 11 },
                            new Grade { Name = "Grade 12", Level = 12 }
                        };
                        context.Grades.AddRange(grades);
                        context.SaveChanges();
                    }

                    // Seed Subjects
                    if (!context.Subjects.Any())
                    {
                        var subjects = new List<Subject>
                        {
                            new Subject { Name = "English (First Additional Language)", Code = "ENG", Category = SubjectCategory.Core },
                            new Subject { Name = "isiZulu (Home Language)", Code = "ZUL", Category = SubjectCategory.Core },
                            new Subject { Name = "Life Orientation", Code = "LO", Category = SubjectCategory.Core },
                            new Subject { Name = "Mathematics", Code = "MATH", Category = SubjectCategory.Core },
                            new Subject { Name = "Natural Science", Code = "NSCI", Category = SubjectCategory.Core },
                            new Subject { Name = "Social Science", Code = "SSCI", Category = SubjectCategory.Core },
                            new Subject { Name = "Creative Arts", Code = "CART", Category = SubjectCategory.Core },
                            new Subject { Name = "Economic Management Science", Code = "EMS", Category = SubjectCategory.Core },
                            new Subject { Name = "Technology", Code = "TECH", Category = SubjectCategory.Core },
                            new Subject { Name = "Mathematical Literacy", Code = "MLIT", Category = SubjectCategory.Elective },
                            new Subject { Name = "Physical Sciences", Code = "PHYS", Category = SubjectCategory.Elective },
                            new Subject { Name = "Life Sciences", Code = "LIFE", Category = SubjectCategory.Elective },
                            new Subject { Name = "History", Code = "HIST", Category = SubjectCategory.Elective },
                            new Subject { Name = "Geography", Code = "GEOG", Category = SubjectCategory.Elective },
                            new Subject { Name = "Accounting", Code = "ACCT", Category = SubjectCategory.Elective },
                            new Subject { Name = "Business Studies", Code = "BSTD", Category = SubjectCategory.Elective },
                            new Subject { Name = "Economics", Code = "ECON", Category = SubjectCategory.Elective },
                            new Subject { Name = "Agricultural Sciences", Code = "AGRI", Category = SubjectCategory.Elective },
                            new Subject { Name = "Computer Applications Technology", Code = "CAT", Category = SubjectCategory.Technology },
                            new Subject { Name = "Information Technology", Code = "IT", Category = SubjectCategory.Technology }
                        };
                        context.Subjects.AddRange(subjects);
                        context.SaveChanges();
                    }

                    // Seed Classes
                    if (!context.Classes.Any())
                    {
                        var grades = context.Grades.ToList();
                        foreach (var grade in grades)
                        {
                            var classCount = (grade.Level == 8 || grade.Level == 9) ? 2 : 3;
                            for (int i = 0; i < classCount; i++)
                            {
                                var className = ((char)('A' + i)).ToString();
                                context.Classes.Add(new Class
                                {
                                    Name = className,
                                    FullName = $"{grade.Name} {className}",
                                    GradeId = grade.Id,
                                    Capacity = 35
                                });
                            }
                        }
                        context.SaveChanges();
                    }

                    // One-time fixup: when the new ExamSession.Status column is added by
                    // auto-migration, every pre-existing row defaults to 0 (Proposed). Older
                    // rows came from the admin-driven flow and had no Status concept — they
                    // were effectively "Published". Mark them as such so they keep appearing
                    // for students/teachers. Identified by absent ProposedAt (new flow always
                    // sets ProposedAt at proposal time).
                    try
                    {
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.ExamSessions SET Status = 2 WHERE Status = 0 AND ProposedAt IS NULL");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ExamSessions status backfill skipped: {ex.Message}");
                    }

                    // Seed Streams
                    if (!context.Streams.Any())
                    {
                        var streams = new List<Stream>
                        {
                            new Stream { Name = "Mathematics, Life Science & Physics", Description = "Science stream with Physics" },
                            new Stream { Name = "Mathematics, Life Science & Agriculture", Description = "Science stream with Agriculture" },
                            new Stream { Name = "Mathematical Literacy, History & Geography", Description = "Commerce/Humanities stream" }
                        };
                        context.Streams.AddRange(streams);
                        context.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database seed error: {ex.Message}");
                }
            }
        }

        // ADD THIS METHOD
        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }
}
