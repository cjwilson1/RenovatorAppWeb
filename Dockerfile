FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["RenovatorApp.Web/RenovatorApp.Web.csproj", "RenovatorApp.Web/"]
COPY ["RenovatorApp.Infrastructure/RenovatorApp.Infrastructure.csproj", "RenovatorApp.Infrastructure/"]
RUN dotnet restore "RenovatorApp.Web/RenovatorApp.Web.csproj"
COPY . .
WORKDIR "/src/RenovatorApp.Web"
RUN dotnet publish "RenovatorApp.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RenovatorApp.Web.dll"]
