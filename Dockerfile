# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Packages.props", "Directory.Build.props", "./"]
COPY ["src/DNTU.SkillBridge.Api/DNTU.SkillBridge.Api.csproj", "src/DNTU.SkillBridge.Api/"]
COPY ["src/DNTU.SkillBridge.Application/DNTU.SkillBridge.Application.csproj", "src/DNTU.SkillBridge.Application/"]
COPY ["src/DNTU.SkillBridge.Domain/DNTU.SkillBridge.Domain.csproj", "src/DNTU.SkillBridge.Domain/"]
COPY ["src/DNTU.SkillBridge.Infrastructure/DNTU.SkillBridge.Infrastructure.csproj", "src/DNTU.SkillBridge.Infrastructure/"]
RUN dotnet restore "src/DNTU.SkillBridge.Api/DNTU.SkillBridge.Api.csproj"
COPY . .
RUN dotnet publish "src/DNTU.SkillBridge.Api/DNTU.SkillBridge.Api.csproj" --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd --system skillbridge && useradd --system --gid skillbridge --no-create-home skillbridge
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER skillbridge
ENTRYPOINT ["dotnet", "DNTU.SkillBridge.Api.dll"]
