# Use the official .NET image as a base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Install PostgreSQL client libraries
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libpq-dev \
    && rm -rf /var/lib/apt/lists/*

# Use the .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["MyMvcApp.csproj", "./"]
RUN dotnet restore "MyMvcApp.csproj"

# Copy the rest of the application files and build the app
COPY . .
RUN dotnet build "MyMvcApp.csproj" -c Release -o /app/build

# Publish the application to a folder
FROM build AS publish
RUN dotnet publish "MyMvcApp.csproj" -c Release -o /app/publish

# Create the final image for deployment
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyMvcApp.dll"]
