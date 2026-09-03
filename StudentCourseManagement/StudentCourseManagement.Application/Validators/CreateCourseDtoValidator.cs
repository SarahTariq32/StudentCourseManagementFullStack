using FluentValidation;
using StudentCourseManagement.Application.DTOs;

namespace StudentCourseManagement.Application.Validators;

public class CreateCourseDtoValidator : AbstractValidator<CreateCourseDto>
{
    public CreateCourseDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Course name is required.")
            .Length(2, 100).WithMessage("Course name must be between 2 and 100 characters.");

        RuleFor(x => x.Credits)
            .GreaterThan(0).WithMessage("Credits must be greater than 0.");

    }
}
