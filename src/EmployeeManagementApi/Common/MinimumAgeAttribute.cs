using System.ComponentModel.DataAnnotations;

namespace EmployeeManagementApi.Common;

// Valida que la fecha de nacimiento corresponda a una edad mínima (18 años por defecto).
public class MinimumAgeAttribute : ValidationAttribute
{
    private readonly int _minimumAge;

    public MinimumAgeAttribute(int minimumAge = 18)
    {
        _minimumAge = minimumAge;
        ErrorMessage = $"El empleado debe tener al menos {minimumAge} años.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateTime birthDate)
            return ValidationResult.Success;

        var age = DateTime.Today.Year - birthDate.Year;
        if (birthDate.Date > DateTime.Today.AddYears(-age)) age--;

        return age >= _minimumAge
            ? ValidationResult.Success
            : new ValidationResult(ErrorMessage);
    }
}
