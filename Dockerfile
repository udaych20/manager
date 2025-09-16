# Use the official Microsoft .NET SDK image to build the project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy the csproj file and restore any NuGet packages
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the project files
COPY . ./

# Publish the application to the /app/out directory
RUN dotnet publish -c Release -o out

# Use the official Microsoft .NET runtime image to run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy the published application from the build environment to the runtime environment
COPY --from=build-env /app/out .

# Expose the port the application is listening on
EXPOSE 80
EXPOSE 443

# Set the entry point for the container to run the application
ENTRYPOINT ["dotnet", "systems-manager.dll"]
