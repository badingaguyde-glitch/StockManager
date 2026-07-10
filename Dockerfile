# 1. Derleme (Build) Aşaması
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyalarını (.csproj) kopyala ve restore et
COPY ["StockManager.Server/StockManager.Server.csproj", "StockManager.Server/"]
RUN dotnet restore "StockManager.Server/StockManager.Server.csproj"

# Kalan tüm dosyaları kopyala ve yayınla (Publish)
COPY . .
WORKDIR "/src/StockManager.Server"
RUN dotnet publish "StockManager.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Çalıştırma (Runtime) Aşaması
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render varsayılan olarak 10000 portunu dinler, ASP.NET Core'u buraya bağlıyoruz
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "StockManager.Server.dll"]
