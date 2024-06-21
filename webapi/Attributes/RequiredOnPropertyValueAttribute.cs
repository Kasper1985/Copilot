using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace WebApi.Attributes;

/// <summary>
/// If the other property is set to the expected value, then this property is required
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class RequiredOnPropertyValueAttribute (string otherPropertyName, object? otherPropertyValue, bool notEmptyOrWhitespace = true) : ValidationAttribute
{
    /// <summary>
    /// Name of the other property
    /// </summary>
    private string OtherPropertyName { get; } = otherPropertyName;

    /// <summary>
    /// Value of the other property when this property is required
    /// </summary>
    private object? OtherPropertyValue { get; } = otherPropertyValue;

    /// <summary>
    /// True to make sure that the value is not empty or whitespace when required
    /// </summary>
    private bool NotEmptyOrWhitespace { get; } = notEmptyOrWhitespace;
    
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var otherPropertyInfo = validationContext.ObjectType.GetRuntimeProperty(OtherPropertyName);
        
        // If the other property is not found, return an error
        if (otherPropertyInfo == null)
            return new ValidationResult($"Unknown other property name '{OtherPropertyName}'.");
        
        var otherPropertyValue = otherPropertyInfo.GetValue(validationContext.ObjectInstance, null);
        
        // If the other property is not set to the expected value, then this property, or it's value is irrelevant
        if (!Equals(OtherPropertyValue, otherPropertyValue))
            return ValidationResult.Success; 
        
        // If the other property is set to the expected value, then this property is required
        if (value is null)
            return new ValidationResult($"Property '{validationContext.DisplayName}' is required when '{OtherPropertyName}' is '{OtherPropertyValue}'.");
        if (NotEmptyOrWhitespace && string.IsNullOrWhiteSpace(value.ToString()))
            return new ValidationResult($"Property '{validationContext.DisplayName}' must not be empty or whitespace when '{OtherPropertyName}' is '{OtherPropertyValue}'.");
        
        return ValidationResult.Success;
    }
}
