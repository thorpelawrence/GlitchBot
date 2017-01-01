FROM mono:latest
ADD ./GlitchBot /glitchbot
RUN xbuild /glitchbot/GlitchBot.csproj
CMD ["mono", "/glitchbot/bin/Debug/GlitchBot.exe"]