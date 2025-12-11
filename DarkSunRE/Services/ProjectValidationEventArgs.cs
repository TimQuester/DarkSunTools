using DarkSunRE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DarkSunRE.Services
{
    public class ProjectValidationEventArgs : ProjectEventArgs
    {
        public string[] ValidationErrors { get; }
        public bool IsValid => ValidationErrors.Length == 0;

        public ProjectValidationEventArgs(Project project, string[] validationErrors)
            : base(ProjectAction.ValidationError, project)
        {
            ValidationErrors = validationErrors;
        }
    }
}
