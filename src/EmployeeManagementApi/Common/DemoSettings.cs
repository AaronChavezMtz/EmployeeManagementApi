namespace EmployeeManagementApi.Common;

// Se enlaza a la sección "Demo". ProtectedUsers son cuentas que los admins públicos de la demo
// no pueden modificar (separadas por coma); Program.cs agrega también InitialAdmin y SuperAdmin.
// OwnerUsers lo llena Program.cs con InitialAdmin y SuperAdmin: las cuentas privadas del dueño,
// que sí pueden editar cuentas protegidas y reiniciar la demo.
public class DemoSettings
{
    public bool Enabled { get; set; }
    public string ProtectedUsers { get; set; } = string.Empty;
    public string OwnerUsers { get; set; } = string.Empty;

    public bool IsProtected(string username) => Contains(ProtectedUsers, username);

    public bool IsOwner(string username) => Contains(OwnerUsers, username);

    public bool CanReset(string username) => Enabled && IsOwner(username);

    // Una cuenta protegida solo la edita un dueño; las demás, cualquier admin.
    public bool CanEdit(string editor, string target) => !IsProtected(target) || IsOwner(editor);

    private static bool Contains(string list, string username) =>
        list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(username, StringComparer.OrdinalIgnoreCase);
}
