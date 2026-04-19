using System;
using System.Collections.Generic;

namespace ElevateED.ViewModels
{
    public class ManageUsersViewModel
    {
        public List<TeacherUserViewModel> Teachers { get; set; }
        public List<StudentUserViewModel> Students { get; set; }
    }

    public class TeacherUserViewModel
    {
        public int Id { get; set; }
        public string StaffNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Qualification { get; set; }
        public string Subjects { get; set; }
        public string Grades { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StudentUserViewModel
    {
        public int UserId { get; set; }
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string CellPhone { get; set; }
        public string GradeApplyingFor { get; set; }
        public string StreamChoice { get; set; }
        public string Status { get; set; }
        public DateTime ApplicationDate { get; set; }
    }
}