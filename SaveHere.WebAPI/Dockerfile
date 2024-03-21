FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8580
#EXPOSE 8581

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SaveHere.WebAPI/SaveHere.WebAPI.csproj", "SaveHere.WebAPI/"]
RUN dotnet restore "./SaveHere.WebAPI/SaveHere.WebAPI.csproj"
COPY . .
WORKDIR "/src/SaveHere.WebAPI"
RUN dotnet build "./SaveHere.WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SaveHere.WebAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SaveHere.WebAPI.dll"]