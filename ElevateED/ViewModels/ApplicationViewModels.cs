using ElevateED.Models;
using System.Collections.Generic;
using System.Linq;

namespace ElevateED.ViewModels
{
    public class ApplicationEvaluationViewModel
    {
        public ApplicationCycle Cycle { get; set; }
        public List<Applicant> Applicants { get; set; }
        public List<IGrouping<string, Applicant>> GradeGroups { get; set; }
    }

    public class BulkApprovalViewModel
    {
        public string SelectedApplicantIds { get; set; }
        public int? ClassId { get; set; }
        public string RejectionReason { get; set; }  // ADD THIS
    }

    public class ClassAllocationViewModel
    {
        public List<Student> UnallocatedStudents { get; set; }
        public List<Class> AvailableClasses { get; set; }
        public List<Grade> Grades { get; set; }  // ADD THIS
        public int? SelectedGradeId { get; set; }  // ADD THIS
        public Dictionary<int, int> ClassStudentCounts { get; set; }
    }

    public class ClassStructureViewModel
    {
        public List<Grade> Grades { get; set; }
        public List<Class> Classes { get; set; }
        public List<Teacher> Teachers { get; set; }
    }

    public class ClassDetailsViewModel
    {
        public Class Class { get; set; }
        public List<Subject> Subjects { get; set; }
        public List<TeacherSubjectAssignment> CurrentAssignments { get; set; }
        public List<Teacher> QualifiedTeachers { get; set; }
    }
}