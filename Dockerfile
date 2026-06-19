# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["UavPms.WebApi/UavPms.WebApi.csproj", "UavPms.WebApi/"]
COPY ["UavPms.Core/UavPms.Core.csproj", "UavPms.Core/"]
COPY ["UavPms.Application/UavPms.Application.csproj", "UavPms.Application/"]
COPY ["UavPms.Infrastructure/UavPms.Infrastructure.csproj", "UavPms.Infrastructure/"]

RUN dotnet restore "UavPms.WebApi/UavPms.WebApi.csproj"

# Copy remaining source code
COPY . .
WORKDIR "/src/UavPms.WebApi"
RUN dotnet build "UavPms.WebApi.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "UavPms.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Final Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose HTTP port
EXPOSE 8080

ENTRYPOINT ["dotnet", "UavPms.WebApi.dll"]
