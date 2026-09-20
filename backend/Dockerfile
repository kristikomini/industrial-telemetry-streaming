FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MotorValley.sln ./
COPY MotorValley.Backend/MotorValley.Backend.csproj MotorValley.Backend/
RUN dotnet restore MotorValley.Backend/MotorValley.Backend.csproj

COPY MotorValley.Backend/ MotorValley.Backend/
WORKDIR /src/MotorValley.Backend
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "MotorValley.Backend.dll"]
