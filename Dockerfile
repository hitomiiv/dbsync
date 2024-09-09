FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

COPY ./sqlorder/bin/Release/net8.0/linux-x64/publish .
COPY ./Examples .

ENV PATH="/app:${PATH}"
