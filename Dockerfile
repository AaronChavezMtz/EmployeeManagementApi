# ---------- Etapa 1: build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/EmployeeManagementApi/EmployeeManagementApi.csproj ./EmployeeManagementApi/
RUN dotnet restore ./EmployeeManagementApi/EmployeeManagementApi.csproj

COPY src/EmployeeManagementApi/ ./EmployeeManagementApi/
WORKDIR /src/EmployeeManagementApi
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Etapa 2: runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render inyecta la variable PORT en tiempo de ejecución; Program.cs la lee.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "EmployeeManagementApi.dll"]
