using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FluentValidation;
using StudentCourseManagement.Application.DTOs;

namespace StudentCourseManagement.Application.Validators;

public class StudentQueryParametersValidator : AbstractValidator<StudentQueryParameters>
{
    private static readonly string[] AllowedSortColumns = { "name", "email", "age", "id" };

    public StudentQueryParametersValidator()
    {
        RuleFor(x => x.PageIndex)
            .GreaterThanOrEqualTo(1).WithMessage("Page index must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50).WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x.MinAge)
            .InclusiveBetween(1, 120).When(x => x.MinAge.HasValue)
            .WithMessage("Min age must be between 1 and 120.");

        RuleFor(x => x.MaxAge)
            .InclusiveBetween(1, 120).When(x => x.MaxAge.HasValue)
            .WithMessage("Max age must be between 1 and 120.");

        RuleFor(x => x)
            .Must(x => !x.MinAge.HasValue || !x.MaxAge.HasValue || x.MinAge <= x.MaxAge)
            .WithMessage("Min age cannot be greater than Max age.");

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrEmpty(sortBy) || AllowedSortColumns.Contains(sortBy.ToLower()))
            .WithMessage($"Invalid sort column. Allowed values are: {string.Join(", ", AllowedSortColumns)}.");
    }
}
