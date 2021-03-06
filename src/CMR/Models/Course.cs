﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CMR.Models
{
    public class Course
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20, MinimumLength = 3)]
        public string Code { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; }

        [Required]
        [DataType(DataType.Text)]
        public string Description { get; set; }

        public virtual ICollection<CourseAssignment> CourseAssignments { get; set; }
        public virtual ICollection<Faculty> Faculties { get; set; }
    }
}