# Use the official .NET SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["MyMvcApp.csproj", "./"]
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet build "MyMvcApp.csproj" -c Release -o /app/build

# Publish
RUN dotnet publish "MyMvcApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Install PostgreSQL client (if needed)
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       postgresql-client \
    && rm -rf /var/lib/apt/lists/*

# Create a script to run migrations and start the app
RUN echo '#!/bin/bash\n\
dotnet MyMvcApp.dll' > /app/start.sh \
    && chmod +x /app/start.sh

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Create a non-root user and switch to it
RUN useradd -u 5678 --create-home appuser \
    && chown -R appuser:appuser /app
USER appuser

EXPOSE 8080

CMD ["/app/start.sh"]
