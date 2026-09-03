using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;
using StudentCourseManagement.Application.DTOs;

namespace StudentCourseManagement.Application.Validators;

public class CourseQueryParametersValidator : AbstractValidator<CourseQueryParameters>
{
    private static readonly string[] AllowedSortColumns = { "name", "credits", "id" };

    public CourseQueryParametersValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Page index must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.MinCredits)
            .GreaterThanOrEqualTo(0).When(x => x.MinCredits.HasValue)
            .WithMessage("Min credits cannot be negative.");

        RuleFor(x => x.MaxCredits)
            .GreaterThanOrEqualTo(0).When(x => x.MaxCredits.HasValue)
            .WithMessage("Max credits cannot be negative.");

        RuleFor(x => x)
            .Must(x => !x.MinCredits.HasValue || !x.MaxCredits.HasValue || x.MinCredits <= x.MaxCredits)
            .WithMessage("Min credits cannot be greater than Max credits.");

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrEmpty(sortBy) || AllowedSortColumns.Contains(sortBy.ToLower()))
            .WithMessage($"Invalid sort column. Allowed values are: {string.Join(", ", AllowedSortColumns)}.");
    }
}
