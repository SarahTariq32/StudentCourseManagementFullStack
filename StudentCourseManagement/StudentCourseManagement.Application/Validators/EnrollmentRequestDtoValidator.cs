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
        RuleFor(x => x.CourseId)
            .GreaterThan(0).WithMessage("Course ID must be greater than 0.");

        RuleFor(x => x.Reason)
            .MaximumLength(250).WithMessage("Reason cannot exceed 250 characters.");
    }
}
