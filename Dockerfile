FROM microsoft/dotnet:latest
ADD ./GlitchBot /glitchbot
RUN mkdir hwapp && cd hwapp && dotnet new
RUN dotnet restore && dotnet run
