# https://docs.microsoft.com/en-us/aspnet/mvc/overview/deployment/docker-aspnetmvc
# https://github.com/dotnet/samples/tree/master/framework/docker/MVCRandomAnswerGenerator

FROM microsoft/aspnet:4.7.2

RUN mkdir C:\linker

RUN powershell -NoProfile -Command \
    Import-module IISAdministration; \
    New-IISSite -Name "ASPNET" -PhysicalPath C:\linker -BindingInformation "*:8000:"

EXPOSE 8000

ADD /src/web/bin/Release/Publish/ /linker

# First Publish the web project using visual studio
# To build it: docker build -t linker . 
# To run it: docker run -d -p 8000:8000 --name linker linker
# To see it: open browser to http://localhost:8000
