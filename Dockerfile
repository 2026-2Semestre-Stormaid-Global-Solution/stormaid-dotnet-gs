FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY Stormaid.API/Stormaid.API.csproj Stormaid.API/
COPY Stormaid.Application/Stormaid.Application.csproj Stormaid.Application/
COPY Stormaid.Domain/Stormaid.Domain.csproj Stormaid.Domain/
COPY Stormaid.Infrastructure/Stormaid.Infrastructure.csproj Stormaid.Infrastructure/

RUN dotnet restore Stormaid.API/Stormaid.API.csproj

COPY . .

RUN dotnet publish Stormaid.API/Stormaid.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Stormaid.API.dll"]
