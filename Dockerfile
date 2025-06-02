# Use .NET SDK for building
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base 
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy remaining source code
COPY ["ShareItAPI/ShareItAPI.csproj", "ShareItAPI/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["BusinessObject/BusinessObject.csproj", "BusinessObject/"]
COPY ["Repositories/Repositories.csproj", "Repositories/"]
COPY ["Common/Common.csproj", "Common/"]
COPY ["DataAccess/DataAccess.csproj", "DataAccess/"]
RUN dotnet restore "ShareItAPI/ShareItAPI.csproj"
COPY . .
WORKDIR "/src/ShareItAPI"
RUN dotnet build "ShareItAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build


# Use the .NET Runtime image for running the app
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ShareItAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish    
# Copy published app from build stage

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Start the application
ENV ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "ShareItAPI.dll"]