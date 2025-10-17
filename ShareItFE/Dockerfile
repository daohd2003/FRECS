# Use .NET SDK for building
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base 
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for dependency resolution (ShareItFE only needs BusinessObject and Services)
COPY ["ShareItFE/ShareItFE.csproj", "ShareItFE/"]
COPY ["BusinessObject/BusinessObject.csproj", "BusinessObject/"]
COPY ["Services/Services.csproj", "Services/"]

# Restore dependencies
RUN dotnet restore "ShareItFE/ShareItFE.csproj"

# Copy all source code
COPY . .
WORKDIR "/src/ShareItFE"

# Build the application
RUN dotnet build "ShareItFE.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ShareItFE.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage - runtime
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Start the application
ENV ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "ShareItFE.dll"]