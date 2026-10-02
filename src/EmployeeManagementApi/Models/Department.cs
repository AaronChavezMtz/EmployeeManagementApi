using System.Text.Json.Serialization;

namespace EmployeeManagementApi.Models;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
