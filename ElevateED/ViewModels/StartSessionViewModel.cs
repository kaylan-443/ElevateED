using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace ElevateED.Models.ViewModels
{
    /// <summary>
    /// ViewModel for starting a new attendance session.
    /// </summary>
    public class StartSessionViewModel
    {
        [Required]
        [Display(Name = "Select Class")]
        public int ClassId { get; set; }

        public IEnumerable<SelectListItem> AvailableClasses { get; set; }

        public string OTPCode { get; set; }

        public int SessionId { get; set; }
    }
}
