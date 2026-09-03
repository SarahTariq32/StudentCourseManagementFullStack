using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;
using StudentCourseManagement.Application.DTOs;

namespace StudentCourseManagement.Application.Validators;

public class EnrollmentRequestDtoValidator : AbstractValidator<CreateEnrollmentRequestDto>
{
    public EnrollmentRequestDtoValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CourseId > 0
                     || !string.IsNullOrWhiteSpace(x.CourseName)
                     || (x.Reason != null && x.Reason.Contains("ACCOUNT_CREATION_REQUEST")))
            .WithMessage("Either a valid Course ID, Course Name, or account creation reason is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(250).WithMessage("Reason cannot exceed 250 characters.");
    }
}