using System.ComponentModel.DataAnnotations;

namespace WebApi.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class NotEmptyOrWhitespaceAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        switch (value)
        {
            case null:
            case string s when !string.IsNullOrWhiteSpace(s):
                return ValidationResult.Success;
            case string:
                return new ValidationResult($"'{validationContext.MemberName}' cannot be empty or whitespace.");
            default:
                return new ValidationResult($"'{validationContext.MemberName}' must be a string.");
        }
    }
}
