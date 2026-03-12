# Base image with ASP.NET Core runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy entire project folder (repo root is build context)
COPY homeownerssubdivision-main/. ./homeownerssubdivision-main

# Restore and publish from inside the project folder
WORKDIR /src/homeownerssubdivision-main
RUN dotnet restore "HOMEOWNER.csproj"
RUN dotnet publish "HOMEOWNER.csproj" -c Release -o /app/publish

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HOMEOWNER.dll"]
