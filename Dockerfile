FROM mono:latest
ADD ./GlitchBot /glitchbot
RUN mcs /glitchbot/GlitchBot.cs