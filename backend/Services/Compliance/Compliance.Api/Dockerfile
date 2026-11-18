FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Services/Compliance/Compliance.Api/Compliance.Api.csproj
RUN dotnet publish Services/Compliance/Compliance.Api/Compliance.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=""
EXPOSE 8083 5083
ENTRYPOINT ["dotnet", "Compliance.Api.dll"]
