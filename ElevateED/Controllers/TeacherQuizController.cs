using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using ElevateED.Models;
using OfficeOpenXml;

namespace ElevateED.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class TeacherQuizController : Controller
    {
        private ElevateEDContext _context = new ElevateEDContext();

        // ============================================
        // MANAGE QUESTIONS
        // ============================================

        public ActionResult Index()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            var questions = _context.QuizQuestions
                .Include(q => q.Subject)
                .Include(q => q.Grade)
                .Where(q => q.CreatedBy == teacher.Id && q.IsActive)
                .OrderByDescending(q => q.CreatedAt)
                .ToList();  // ← Execute query FIRST

            return View(questions);
        }

        // ============================================
        // DOWNLOAD TEMPLATE
        // ============================================

        public ActionResult DownloadTemplate()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using (var package = new ExcelPackage())
            {
                // === QUESTIONS SHEET ===
                var worksheet = package.Workbook.Worksheets.Add("Quiz Questions");

                // Header row
                worksheet.Cells[1, 1].Value = "Question Text";
                worksheet.Cells[1, 2].Value = "Option A";
                worksheet.Cells[1, 3].Value = "Option B";
                worksheet.Cells[1, 4].Value = "Option C";
                worksheet.Cells[1, 5].Value = "Option D";
                worksheet.Cells[1, 6].Value = "Correct Answer (A/B/C/D)";
                worksheet.Cells[1, 7].Value = "Subject";
                worksheet.Cells[1, 8].Value = "Grade";

                // Style header
                var headerRange = worksheet.Cells[1, 1, 1, 8];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(30, 60, 114));
                headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);

                // Example row
                worksheet.Cells[2, 1].Value = "What is the capital of France?";
                worksheet.Cells[2, 2].Value = "London";
                worksheet.Cells[2, 3].Value = "Paris";
                worksheet.Cells[2, 4].Value = "Berlin";
                worksheet.Cells[2, 5].Value = "Madrid";
                worksheet.Cells[2, 6].Value = "B";
                worksheet.Cells[2, 7].Value = "Geography";
                worksheet.Cells[2, 8].Value = "Grade 10";

                // Auto-fit columns
                worksheet.Cells.AutoFitColumns();

                // === VALID VALUES SHEET ===
                var notesSheet = package.Workbook.Worksheets.Add("Valid Values");

                notesSheet.Cells[1, 1].Value = "VALID SUBJECT NAMES (use exactly as shown, NOT case-sensitive)";
                notesSheet.Cells[1, 1].Style.Font.Bold = true;
                notesSheet.Cells[1, 1].Style.Font.Size = 12;
                notesSheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                notesSheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(212, 175, 55));

                var subjects = _context.Subjects.OrderBy(s => s.Name).ToList();
                int row = 3;
                foreach (var subject in subjects)
                {
                    notesSheet.Cells[row, 1].Value = subject.Name;
                    notesSheet.Cells[row, 2].Value = subject.Category.ToString();
                    row++;
                }

                notesSheet.Cells[1, 4].Value = "VALID GRADE NAMES (use exactly as shown, NOT case-sensitive)";
                notesSheet.Cells[1, 4].Style.Font.Bold = true;
                notesSheet.Cells[1, 4].Style.Font.Size = 12;
                notesSheet.Cells[1, 4].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                notesSheet.Cells[1, 4].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(212, 175, 55));

                var grades = _context.Grades.OrderBy(g => g.Level).ToList();
                row = 3;
                foreach (var grade in grades)
                {
                    notesSheet.Cells[row, 4].Value = grade.Name;
                    row++;
                }

                // === INSTRUCTIONS SHEET ===
                var instructionsSheet = package.Workbook.Worksheets.Add("Instructions");
                instructionsSheet.Cells[1, 1].Value = "HOW TO FILL THIS TEMPLATE";
                instructionsSheet.Cells[1, 1].Style.Font.Bold = true;
                instructionsSheet.Cells[1, 1].Style.Font.Size = 16;

                instructionsSheet.Cells[3, 1].Value = "1. Fill in your questions on the 'Quiz Questions' sheet";
                instructionsSheet.Cells[4, 1].Value = "2. Each row = one question";
                instructionsSheet.Cells[5, 1].Value = "3. Correct Answer must be A, B, C, or D";
                instructionsSheet.Cells[6, 1].Value = "4. Subject and Grade names are NOT case-sensitive";
                instructionsSheet.Cells[7, 1].Value = "5. Check the 'Valid Values' sheet for correct Subject/Grade names";
                instructionsSheet.Cells[8, 1].Value = "6. Do NOT modify the header row";
                instructionsSheet.Cells[9, 1].Value = "7. Delete the example row before uploading";
                instructionsSheet.Cells[10, 1].Value = "8. Save as .xlsx format before uploading";
                instructionsSheet.Cells[12, 1].Value = "Need help? Contact the administrator.";
                instructionsSheet.Cells[12, 1].Style.Font.Italic = true;

                notesSheet.Cells.AutoFitColumns();
                instructionsSheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "QuizQuestionTemplate.xlsx");
            }
        }

        // ============================================
        // UPLOAD QUESTIONS
        // ============================================

        public ActionResult Upload()
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadQuestions()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Teacher not found" });

            if (Request.Files.Count == 0)
                return Json(new { success = false, message = "Please select a file" });

            var file = Request.Files[0];
            if (file == null || file.ContentLength == 0)
                return Json(new { success = false, message = "Please select a valid file" });

            var result = new UploadResult
            {
                SuccessCount = 0,
                ErrorCount = 0,
                Errors = new List<string>()
            };

            try
            {
                using (var package = new ExcelPackage(file.InputStream))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    var totalRows = worksheet.Dimension.Rows;

                    for (int row = 2; row <= totalRows; row++)
                    {
                        try
                        {
                            var questionText = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            var optionA = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                            var optionB = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                            var optionC = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                            var optionD = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
                            var correctAnswer = worksheet.Cells[row, 6].Value?.ToString()?.Trim()?.ToUpper();
                            var subjectName = worksheet.Cells[row, 7].Value?.ToString()?.Trim();
                            var gradeName = worksheet.Cells[row, 8].Value?.ToString()?.Trim();

                            // Skip empty rows
                            if (string.IsNullOrEmpty(questionText) && string.IsNullOrEmpty(optionA))
                                continue;

                            // Validate
                            var errors = ValidateQuestionRow(questionText, optionA, optionB, optionC, optionD, correctAnswer, subjectName, gradeName);
                            if (errors.Any())
                            {
                                result.ErrorCount++;
                                result.Errors.Add($"Row {row}: {string.Join(", ", errors)}");
                                continue;
                            }

                            // Find subject (CASE-INSENSITIVE)
                            var subject = _context.Subjects
                                .FirstOrDefault(s => s.Name.Equals(subjectName, StringComparison.OrdinalIgnoreCase));

                            if (subject == null)
                            {
                                result.ErrorCount++;
                                result.Errors.Add($"Row {row}: Subject '{subjectName}' not found. Check the 'Valid Values' sheet in the template.");
                                continue;
                            }

                            // Find grade (CASE-INSENSITIVE)
                            var grade = _context.Grades
                                .FirstOrDefault(g => g.Name.Equals(gradeName, StringComparison.OrdinalIgnoreCase));

                            if (grade == null)
                            {
                                result.ErrorCount++;
                                result.Errors.Add($"Row {row}: Grade '{gradeName}' not found. Check the 'Valid Values' sheet in the template.");
                                continue;
                            }

                            // Check for duplicate question (same text, same subject, same teacher)
                            var existingQuestion = _context.QuizQuestions
                                .FirstOrDefault(q => q.QuestionText == questionText
                                    && q.SubjectId == subject.Id
                                    && q.CreatedBy == teacher.Id
                                    && q.IsActive);

                            if (existingQuestion != null)
                            {
                                result.ErrorCount++;
                                result.Errors.Add($"Row {row}: Duplicate question skipped (already exists in your question bank)");
                                continue;
                            }

                            // Create question
                            var question = new QuizQuestion
                            {
                                QuestionText = questionText,
                                OptionA = optionA,
                                OptionB = optionB,
                                OptionC = optionC,
                                OptionD = optionD,
                                CorrectAnswer = correctAnswer,
                                SubjectId = subject.Id,
                                GradeId = grade.Id,
                                CreatedBy = teacher.Id,
                                CreatedAt = DateTime.Now,
                                IsActive = true
                            };

                            _context.QuizQuestions.Add(question);
                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            result.ErrorCount++;
                            result.Errors.Add($"Row {row}: {ex.Message}");
                        }
                    }

                    _context.SaveChanges();
                }

                return Json(new
                {
                    success = true,
                    message = $"Upload complete! {result.SuccessCount} questions added, {result.ErrorCount} errors.",
                    details = result
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error processing file: " + ex.Message });
            }
        }

        private List<string> ValidateQuestionRow(string questionText, string optionA, string optionB,
            string optionC, string optionD, string correctAnswer, string subjectName, string gradeName)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(questionText)) errors.Add("Question text is required");
            if (string.IsNullOrEmpty(optionA)) errors.Add("Option A is required");
            if (string.IsNullOrEmpty(optionB)) errors.Add("Option B is required");
            if (string.IsNullOrEmpty(optionC)) errors.Add("Option C is required");
            if (string.IsNullOrEmpty(optionD)) errors.Add("Option D is required");
            if (string.IsNullOrEmpty(correctAnswer)) errors.Add("Correct answer is required");
            if (string.IsNullOrEmpty(subjectName)) errors.Add("Subject is required");
            if (string.IsNullOrEmpty(gradeName)) errors.Add("Grade is required");

            if (!string.IsNullOrEmpty(correctAnswer) && !new[] { "A", "B", "C", "D" }.Contains(correctAnswer))
                errors.Add("Correct answer must be A, B, C, or D");

            return errors;
        }

        [HttpPost]
        public ActionResult DeleteQuestion(int id)
        {
            var teacher = GetCurrentTeacher();
            if (teacher == null) return Json(new { success = false, message = "Unauthorized" });

            var question = _context.QuizQuestions.Find(id);
            if (question == null || question.CreatedBy != teacher.Id)
                return Json(new { success = false, message = "Question not found" });

            question.IsActive = false;
            _context.SaveChanges();

            return Json(new { success = true, message = "Question deleted" });
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        private Teacher GetCurrentTeacher()
        {
            var staffNumber = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == staffNumber);
            if (user == null) return null;

            return _context.Teachers
                .Include(t => t.SubjectQualifications.Select(sq => sq.Subject))
                .Include(t => t.GradeAssignments.Select(ga => ga.Grade))
                .FirstOrDefault(t => t.UserId == user.Id);
        }

        public class UploadResult
        {
            public int SuccessCount { get; set; }
            public int ErrorCount { get; set; }
            public List<string> Errors { get; set; }
        }
    }
}