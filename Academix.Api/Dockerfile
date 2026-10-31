FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src
COPY ["Academix.Api/Academix.Api.csproj", "Academix.Api/"]
COPY ["Directory.Build.props", "./"]
RUN dotnet restore "Academix.Api/Academix.Api.csproj"
COPY . .
RUN dotnet publish "Academix.Api/Academix.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0.3
RUN apt-get update && apt-get install -y --no-install-recommends fonts-dejavu-core libfontconfig1 libfreetype6 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Academix.Api.dll"]
