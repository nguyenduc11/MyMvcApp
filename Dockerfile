# Use the official .NET image as a base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project files and restore dependencies
COPY ["MyMvcApp/MyMvcApp.csproj", "MyMvcApp/"]
RUN dotnet restore "MyMvcApp/MyMvcApp.csproj"

# Copy the rest of the application files and build the app
COPY . .
WORKDIR "/src/MyMvcApp"
RUN dotnet build "MyMvcApp.csproj" -c Release -o /app/build

# Publish the application to a folder
FROM build AS publish
RUN dotnet publish "MyMvcApp.csproj" -c Release -o /app/publish

# Create the final image for deployment
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyMvcApp.dll"]
