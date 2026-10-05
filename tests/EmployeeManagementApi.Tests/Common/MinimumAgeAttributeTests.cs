using System.ComponentModel.DataAnnotations;
using EmployeeManagementApi.Common;
using FluentAssertions;
using Xunit;

namespace EmployeeManagementApi.Tests.Common;

public class MinimumAgeAttributeTests
{
    private class TestModel
    {
        [MinimumAge(18)]
        public DateTime BirthDate { get; set; }
    }

    private static List<ValidationResult> Validate(TestModel model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ConMenorDeEdad_FallaLaValidacion()
    {
        var model = new TestModel { BirthDate = DateTime.Today.AddYears(-17) };

        var results = Validate(model);

        results.Should().ContainSingle();
    }

    [Fact]
    public void ConExactamente18Años_PasaLaValidacion()
    {
        var model = new TestModel { BirthDate = DateTime.Today.AddYears(-18) };

        var results = Validate(model);

        results.Should().BeEmpty();
    }

    [Fact]
    public void ConMayorDeEdad_PasaLaValidacion()
    {
        var model = new TestModel { BirthDate = DateTime.Today.AddYears(-30) };

        var results = Validate(model);

        results.Should().BeEmpty();
    }
}
