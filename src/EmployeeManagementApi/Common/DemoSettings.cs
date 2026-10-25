namespace EmployeeManagementApi.Common;

// Se enlaza a la sección "Demo". ProtectedUsers son cuentas que nadie puede modificar
// desde la API (separadas por coma); Program.cs agrega también InitialAdmin y SuperAdmin.
// ResetUsers lo llena Program.cs con InitialAdmin y SuperAdmin: solo ellos reinician la demo.
public class DemoSettings
{
    public bool Enabled { get; set; }
    public string ProtectedUsers { get; set; } = string.Empty;
    public string ResetUsers { get; set; } = string.Empty;

    public bool IsProtected(string username) => Contains(ProtectedUsers, username);

    public bool CanReset(string username) => Enabled && Contains(ResetUsers, username);

    private static bool Contains(string list, string username) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(username, StringComparer.OrdinalIgnoreCase);
}
