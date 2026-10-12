# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY ["DocUploaderApi.csproj", "."]
RUN dotnet restore "DocUploaderApi.csproj"

COPY . .
RUN dotnet build "DocUploaderApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "DocUploaderApi.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app
COPY --from=publish /app/publish .

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD dotnet run --no-build --configuration Release || exit 1

EXPOSE 80 443

ENTRYPOINT ["dotnet", "DocUploaderApi.dll"]
