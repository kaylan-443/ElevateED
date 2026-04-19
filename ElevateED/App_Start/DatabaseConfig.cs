using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
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
                            PasswordHash = "Admin@123",
                            Role = UserRole.Admin,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            HasChangedPassword = true
                        };
                        context.Users.Add(admin);
                        context.SaveChanges();
                        System.Diagnostics.Debug.WriteLine("Admin created: ADMIN001 / Admin@123");
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
                        System.Diagnostics.Debug.WriteLine("Grades seeded");
                    }

                    // Seed Subjects
                    if (!context.Subjects.Any())
                    {
                        var subjects = new List<Subject>
                        {
                            // Core Subjects
                            new Subject { Name = "English (First Additional Language)", Code = "ENG", Category = SubjectCategory.Core },
                            new Subject { Name = "isiZulu (Home Language)", Code = "ZUL", Category = SubjectCategory.Core },
                            new Subject { Name = "Life Orientation", Code = "LO", Category = SubjectCategory.Core },
                            new Subject { Name = "Mathematics", Code = "MATH", Category = SubjectCategory.Core },
                            
                            // Grade 8-9 Specific
                            new Subject { Name = "Natural Science", Code = "NSCI", Category = SubjectCategory.Core },
                            new Subject { Name = "Social Science", Code = "SSCI", Category = SubjectCategory.Core },
                            new Subject { Name = "Creative Arts", Code = "CART", Category = SubjectCategory.Core },
                            new Subject { Name = "Economic Management Science", Code = "EMS", Category = SubjectCategory.Core },
                            new Subject { Name = "Technology", Code = "TECH", Category = SubjectCategory.Core },
                            
                            // Elective Subjects
                            new Subject { Name = "Mathematical Literacy", Code = "MLIT", Category = SubjectCategory.Elective },
                            new Subject { Name = "Physical Sciences", Code = "PHYS", Category = SubjectCategory.Elective },
                            new Subject { Name = "Life Sciences", Code = "LIFE", Category = SubjectCategory.Elective },
                            new Subject { Name = "History", Code = "HIST", Category = SubjectCategory.Elective },
                            new Subject { Name = "Geography", Code = "GEOG", Category = SubjectCategory.Elective },
                            new Subject { Name = "Accounting", Code = "ACCT", Category = SubjectCategory.Elective },
                            new Subject { Name = "Business Studies", Code = "BSTD", Category = SubjectCategory.Elective },
                            new Subject { Name = "Economics", Code = "ECON", Category = SubjectCategory.Elective },
                            new Subject { Name = "Agricultural Sciences", Code = "AGRI", Category = SubjectCategory.Elective },
                            
                            // Technology Subjects
                            new Subject { Name = "Computer Applications Technology", Code = "CAT", Category = SubjectCategory.Technology },
                            new Subject { Name = "Information Technology", Code = "IT", Category = SubjectCategory.Technology }
                        };
                        context.Subjects.AddRange(subjects);
                        context.SaveChanges();
                        System.Diagnostics.Debug.WriteLine("Subjects seeded");
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
                        System.Diagnostics.Debug.WriteLine("Classes seeded");
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
                        System.Diagnostics.Debug.WriteLine("Streams seeded");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database seed error: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }
    }
}