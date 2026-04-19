using System;
using System.Net;
using System.Net.Mail;
using System.Configuration;

namespace ElevateED.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly bool _enableSsl;

        public EmailService()
        {
            _smtpServer = ConfigurationManager.AppSettings["SmtpServer"];
            _smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            _smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"];
            _smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
            _fromEmail = ConfigurationManager.AppSettings["FromEmail"];
            _fromName = ConfigurationManager.AppSettings["FromName"] ?? "ElevateED - Mpiyakhe High School";
            _enableSsl = bool.Parse(ConfigurationManager.AppSettings["EnableSsl"] ?? "true");
        }

        public void SendApplicationSubmittedEmail(string toEmail, string studentName, string studentNumber, string tempPassword)
        {
            var subject = "Application Submitted - ElevateED (Mpiyakhe High School)";
            var body = @"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                        <div style='background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0;'>
                            <h1 style='margin: 0;'>ElevateED</h1>
                            <p style='margin: 5px 0 0 0;'>Mpiyakhe High School</p>
                        </div>
                        
                        <div style='padding: 20px; background: #f9f9f9;'>
                            <h2 style='color: #1e3c72;'>Dear " + studentName + @"</h2>
                            
                            <p>Thank you for submitting your application to <strong>Mpiyakhe High School</strong> through ElevateED.</p>
                            
                            <div style='background: white; padding: 15px; border-left: 4px solid #2a5298; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #1e3c72;'>Your Login Credentials</h3>
                                <p><strong>Student Number:</strong> <span style='font-family: monospace; font-size: 18px; color: #2a5298;'>" + studentNumber + @"</span></p>
                                <p><strong>Temporary Password:</strong> <span style='font-family: monospace; font-size: 18px; color: #2a5298;'>" + tempPassword + @"</span></p>
                            </div>
                            
                            <p>You can use these credentials to log in and track your application status at any time.</p>
                            
                            <div style='background: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <strong>Important:</strong> Please change your password after your first login for security purposes.
                            </div>
                            
                            <p>Your application is currently <strong>under review</strong>. You will receive another email once a decision has been made.</p>
                            
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'/>
                            
                            <p style='font-size: 12px; color: #666;'>
                                This is an automated message from ElevateED School Management System.<br/>
                                Mpiyakhe High School<br/>
                                <em>Empowering Future Leaders</em>
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            SendEmail(toEmail, subject, body);
        }

        public void SendApplicationApprovedEmail(string toEmail, string studentName, string studentNumber, string classAssigned)
        {
            var subject = "Application Approved - Welcome to Mpiyakhe High School!";

            // Parse the class assigned to extract grade and class section
            string grade = classAssigned;
            string className = "";

            // Example: "Grade 10A" -> Grade: "Grade 10", Class: "A"
            if (!string.IsNullOrEmpty(classAssigned) && classAssigned.Contains(" "))
            {
                var parts = classAssigned.Split(' ');
                if (parts.Length >= 2)
                {
                    grade = parts[0] + " " + parts[1]; // "Grade 10"
                    className = parts.Length >= 3 ? parts[2] : parts[1]; // "A" or "B" or "C"
                }
            }

            var body = @"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                        <div style='background: linear-gradient(135deg, #28a745 0%, #20c997 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0;'>
                            <h1 style='margin: 0;'>🎉 Congratulations!</h1>
                            <p style='margin: 5px 0 0 0;'>Mpiyakhe High School</p>
                        </div>
                        
                        <div style='padding: 20px; background: #f9f9f9;'>
                            <h2 style='color: #28a745;'>Dear " + studentName + @"</h2>
                            
                            <p>We are delighted to inform you that your application to <strong>Mpiyakhe High School</strong> has been <strong style='color: #28a745;'>APPROVED</strong>!</p>
                            
                            <div style='background: white; padding: 15px; border-left: 4px solid #28a745; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #28a745;'>Your Acceptance Details</h3>
                                <p><strong>Student Number:</strong> <span style='font-family: monospace; font-size: 16px;'>" + studentNumber + @"</span></p>
                                <p><strong>Grade Assigned:</strong> " + grade + @"</p>
                                <p><strong>Class:</strong> " + className + @"</p>
                                <p><strong>Full Class Name:</strong> " + classAssigned + @"</p>
                                <p><strong>Status:</strong> <span style='color: #28a745; font-weight: bold;'>ENROLLED</span></p>
                            </div>
                            
                            <div style='background: #e8f4f8; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <h4 style='margin-top: 0; color: #1e3c72;'>📚 What's Next?</h4>
                                <ol style='margin-bottom: 0;'>
                                    <li>Log in to your student portal using your credentials</li>
                                    <li>Complete your student profile</li>
                                    <li>View your class timetable</li>
                                    <li>Check for any additional requirements</li>
                                    <li>Attend the orientation session (date to be announced)</li>
                                </ol>
                            </div>
                            
                            <div style='background: #d4edda; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #28a745;'>
                                <strong style='color: #155724;'>🎒 First Day Information:</strong>
                                <p style='margin: 10px 0 0 0;'>Please report to the school administration office on your first day. Your class teacher will be waiting to welcome you to " + classAssigned + @".</p>
                            </div>
                            
                            <p>Please log in to your portal to complete the registration process and view your class details.</p>
                            
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'/>
                            
                            <p style='font-size: 12px; color: #666;'>
                                This is an automated message from ElevateED School Management System.<br/>
                                Mpiyakhe High School<br/>
                                <em>Empowering Future Leaders</em>
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            SendEmail(toEmail, subject, body);
        }

        // Overload for backward compatibility (if needed)
        public void SendApplicationApprovedEmail(string toEmail, string studentName, string studentNumber, string gradeAssigned, string className)
        {
            string fullClass = gradeAssigned + " " + className;
            SendApplicationApprovedEmail(toEmail, studentName, studentNumber, fullClass);
        }

        public void SendTeacherRegistrationEmail(string toEmail, string teacherName, string staffNumber, string tempPassword, string grades, string subjects)
        {
            var subject = "Welcome to Mpiyakhe High School - Staff Registration";
            var body = @"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                        <div style='background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0;'>
                            <h1 style='margin: 0;'>ElevateED</h1>
                            <p style='margin: 5px 0 0 0;'>Mpiyakhe High School</p>
                        </div>
                        
                        <div style='padding: 20px; background: #f9f9f9;'>
                            <h2 style='color: #1e3c72;'>Welcome, " + teacherName + @"!</h2>
                            
                            <p>You have been successfully registered as a teacher at <strong>Mpiyakhe High School</strong>.</p>
                            
                            <div style='background: white; padding: 15px; border-left: 4px solid #28a745; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #28a745;'>Your Login Credentials</h3>
                                <p><strong>Staff Number:</strong> <span style='font-family: monospace; font-size: 18px; color: #2a5298;'>" + staffNumber + @"</span></p>
                                <p><strong>Temporary Password:</strong> <span style='font-family: monospace; font-size: 18px; color: #2a5298;'>" + tempPassword + @"</span></p>
                            </div>
                            
                            <div style='background: #e8f4f8; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <h4 style='margin-top: 0; color: #1e3c72;'>Teaching Assignment</h4>
                                <p><strong>Subjects:</strong> " + subjects + @"</p>
                                <p><strong>Grades:</strong> " + grades + @"</p>
                            </div>
                            
                            <div style='background: #fff3cd; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                                <strong>Important:</strong> Please change your password after your first login for security purposes.
                            </div>
                            
                            <p>Please log in to your portal to complete your profile and view your class assignments.</p>
                            
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'/>
                            
                            <p style='font-size: 12px; color: #666;'>
                                This is an automated message from ElevateED School Management System.<br/>
                                Mpiyakhe High School<br/>
                                <em>Empowering Future Leaders</em>
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            SendEmail(toEmail, subject, body);
        }

        // Send Rejection Email
        public void SendApplicationRejectedEmail(string toEmail, string studentName, string studentNumber, string rejectionReason)
        {
            var subject = "Application Status Update - Mpiyakhe High School";
            var body = @"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                        <div style='background: linear-gradient(135deg, #dc3545 0%, #c82333 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0;'>
                            <h1 style='margin: 0;'>ElevateED</h1>
                            <p style='margin: 5px 0 0 0;'>Mpiyakhe High School</p>
                        </div>
                        
                        <div style='padding: 20px; background: #f9f9f9;'>
                            <h2 style='color: #dc3545;'>Dear " + studentName + @"</h2>
                            
                            <p>Thank you for your interest in <strong>Mpiyakhe High School</strong>.</p>
                            
                            <div style='background: white; padding: 15px; border-left: 4px solid #dc3545; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #dc3545;'>Application Status: REJECTED</h3>
                                <p><strong>Student Number:</strong> " + studentNumber + @"</p>
                                <p><strong>Status:</strong> <span style='color: #dc3545; font-weight: bold;'>Not Accepted</span></p>
                            </div>
                            
                            <div style='background: #f8d7da; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #dc3545;'>
                                <strong style='color: #721c24;'>Reason for Rejection:</strong>
                                <p style='margin: 10px 0 0 0;'>" + (string.IsNullOrEmpty(rejectionReason) ? "No specific reason provided. Please contact the school for more information." : rejectionReason) + @"</p>
                            </div>
                            
                            <p>If you have any questions regarding this decision, please contact the school administration for further clarification.</p>
                            
                            <p>You may reapply for the next academic year if you meet the requirements.</p>
                            
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'/>
                            
                            <p style='font-size: 12px; color: #666;'>
                                This is an automated message from ElevateED School Management System.<br/>
                                Mpiyakhe High School<br/>
                                <em>Empowering Future Leaders</em>
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            SendEmail(toEmail, subject, body);
        }

        // Send Class Assignment Email to Teacher
        public void SendClassAssignmentEmail(string toEmail, string teacherName, string className, string grade, string subjects)
        {
            var subject = "New Class Assignment - ElevateED";
            var body = @"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                        <div style='background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); color: white; padding: 20px; text-align: center; border-radius: 10px 10px 0 0;'>
                            <h1 style='margin: 0;'>ElevateED</h1>
                            <p style='margin: 5px 0 0 0;'>Mpiyakhe High School</p>
                        </div>
                        
                        <div style='padding: 20px; background: #f9f9f9;'>
                            <h2 style='color: #1e3c72;'>Dear " + teacherName + @"</h2>
                            
                            <p>You have been assigned to teach a new class.</p>
                            
                            <div style='background: white; padding: 15px; border-left: 4px solid #28a745; margin: 20px 0;'>
                                <h3 style='margin-top: 0; color: #28a745;'>Class Assignment Details</h3>
                                <p><strong>Class:</strong> " + className + @"</p>
                                <p><strong>Grade:</strong> " + grade + @"</p>
                                <p><strong>Subjects:</strong> " + subjects + @"</p>
                            </div>
                            
                            <p>Please log in to your teacher portal to view the class roster and begin preparing your lessons.</p>
                            
                            <hr style='border: none; border-top: 1px solid #ddd; margin: 20px 0;'/>
                            
                            <p style='font-size: 12px; color: #666;'>
                                This is an automated message from ElevateED School Management System.<br/>
                                Mpiyakhe High School
                            </p>
                        </div>
                    </div>
                </body>
                </html>";

            SendEmail(toEmail, subject, body);
        }

        private void SendEmail(string toEmail, string subject, string body)
        {
            try
            {
                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.EnableSsl = _enableSsl;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 10000; // 10 seconds

                    var message = new MailMessage
                    {
                        From = new MailAddress(_fromEmail, _fromName),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    };

                    message.To.Add(toEmail);

                    // Log attempt
                    System.Diagnostics.Debug.WriteLine($"Sending email to: {toEmail}");
                    System.Diagnostics.Debug.WriteLine($"SMTP Server: {_smtpServer}:{_smtpPort}");
                    System.Diagnostics.Debug.WriteLine($"SSL Enabled: {_enableSsl}");
                    System.Diagnostics.Debug.WriteLine($"From: {_fromEmail}");

                    client.Send(message);

                    System.Diagnostics.Debug.WriteLine("Email sent successfully!");
                }
            }
            catch (SmtpException smtpEx)
            {
                System.Diagnostics.Debug.WriteLine($"SMTP Error: {smtpEx.Message}");
                System.Diagnostics.Debug.WriteLine($"Status Code: {smtpEx.StatusCode}");
                if (smtpEx.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {smtpEx.InnerException.Message}");
                }
                throw new Exception($"Failed to send email: {smtpEx.Message}", smtpEx);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email Error: {ex.Message}");
                throw;
            }
        }

        public void SendCustomEmail(string toEmail, string subject, string body)
        {
            SendEmail(toEmail, subject, body);
        }
    }
}