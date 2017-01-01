FROM mono:4.4.2
ADD ./GlitchBot /glitchbot
RUN xbuild /glitchbot/GlitchBot.csproj
CMD ["mono", "/glitchbot/bin/Debug/GlitchBot.exe"]