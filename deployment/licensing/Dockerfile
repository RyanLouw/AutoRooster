FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/SchoolExamScheduler.LicenceServer --locked-mode
RUN dotnet publish src/SchoolExamScheduler.LicenceServer -c Release -r linux-x64 --self-contained false --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir /data && chown app:app /data && chmod 700 /data
COPY --from=build /out .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production Licensing__DataPath=/data
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolExamScheduler.LicenceServer.dll"]
