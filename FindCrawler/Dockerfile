FROM mcr.microsoft.com/dotnet/core/sdk AS build-env

COPY gmailConfig.json ./

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime
COPY --from=build-env /out ./
ENTRYPOINT ["dotnet", "FindCrawler.dll"]