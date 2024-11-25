# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy csproj and restore dependencies
COPY *.csproj .
RUN dotnet restore --use-current-runtime

# Copy the rest of the files
COPY . .

# Build and publish the app
RUN dotnet publish -c Release -o /app --no-restore --use-current-runtime

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy the published files from the build stage
COPY --from=build /app .

# Install PostgreSQL client (if needed)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libpq-dev \
    && rm -rf /var/lib/apt/lists/*

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Create a non-root user and switch to it
RUN useradd -u 5678 --create-home appuser
USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "MyMvcApp.dll"]
