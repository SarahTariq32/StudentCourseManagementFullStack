using FluentValidation;
using StudentCourseManagement.Application.DTOs;

namespace StudentCourseManagement.Application.Validators;

public class CreateStudentDtoValidator : AbstractValidator<CreateStudentDto>
{
    public CreateStudentDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Student name is required.")
            .Length(2, 100).WithMessage("Student name must be between 2 and 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Age)
            .InclusiveBetween(1, 120).WithMessage("Age must be between 1 and 120.");
    }
}
