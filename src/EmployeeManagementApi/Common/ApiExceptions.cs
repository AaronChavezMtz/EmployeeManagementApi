namespace EmployeeManagementApi.Common;

// Excepciones de negocio propias. El middleware de errores las traduce a códigos HTTP.
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
