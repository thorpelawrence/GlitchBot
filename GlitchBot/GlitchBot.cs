﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Discord;
using Discord.Commands;
using Discord.Audio;
using NAudio;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Threading;

class Program
{
    static float musicVolume = 0.1f;

    static void Main(string[] args)
    {
        var client = new DiscordClient(x =>
        {
            x.LogLevel = LogSeverity.Info;
            x.LogHandler = Log;
        });

        client.UsingCommands(x =>
        {
            x.PrefixChar = '?';
            x.AllowMentionPrefix = true;
            x.HelpMode = HelpMode.Public;
        });

        client.UsingAudio(x =>
        {
            x.Mode = AudioMode.Outgoing;
        });

        var commands = client.GetService<CommandService>();

        commands.CreateCommand("image")
            .Alias("i", "img")
            .Description("Search Google for an image")
            .Parameter("query", ParameterType.Multiple)
            .Do(async e =>
            {
                await e.Channel.SendIsTyping();
                var query = e.GetArg("query");
                await e.Channel.SendMessage(GetImageResultUrl(query));
            });

        commands.CreateCommand("randomimage")
            .Alias("ri", "rimg", "randimg")
            .Description("Search Google for a random image")
            .Parameter("query", ParameterType.Multiple)
            .Do(async e =>
            {
                await e.Channel.SendIsTyping();
                var query = string.Join(" ", e.Args);
                await e.Channel.SendMessage(GetImageResultUrl(query, true));
            });

        commands.CreateCommand("playmusic")
            .Alias("music", "play", "m", "p")
            .Description("Play music in voice channel")
            .Parameter("searchterm", ParameterType.Multiple)
            .Do(async e =>
            {
                string searchterm = string.Join(" ", e.Args);
                if (!string.IsNullOrWhiteSpace(searchterm))
                {
                    var voiceChannel = e.Server.VoiceChannels.FirstOrDefault();
                    var voiceClient = await client.GetService<AudioService>()
                        .Join(voiceChannel);
                    var files = GetMusicFiles(searchterm);
                    if (files.Count <= 0)
                        await e.Channel.SendMessage("No files found for that search term");
                    await Task.Run(() => PlayMusic(files, ref client, ref voiceClient, e.Channel));
                    await voiceClient.Disconnect();
                }
                else await e.Channel.SendMessage("Search term must be entered");
            });

        commands.CreateCommand("listmusic")
            .Alias("list", "l")
            .Description("List music files available")
            .Parameter("searchterm", ParameterType.Multiple)
            .Do(async e =>
            {
                string searchterm = string.Join(" ", e.Args);
                if (!string.IsNullOrWhiteSpace(searchterm))
                {
                    var files = GetMusicFiles(searchterm);
                    files = files.Select(file => file = Path.GetFileName(file)).ToList();
                    if (files.Count <= 0)
                        await e.Channel.SendMessage("No files found for that search term");
                    await e.Channel.SendMessage(string.Join(Environment.NewLine, files));
                }
                else await e.Channel.SendMessage("Search term must be entered");
            });

        commands.CreateCommand("volume")
            .Alias("vol", "v")
            .Description("Set volume of music")
            .Parameter("volume", ParameterType.Optional)
            .Do(async e =>
            {
                float volume;
                if (e.GetArg("volume") != null && float.TryParse(e.GetArg("volume"), out volume))
                {
                    if (volume > 1) volume /= 100;
                    if (volume > 1) volume = 1;
                    if (volume < 0) volume = 0;
                    musicVolume = volume;
                    await e.Channel.SendMessage($"Volume: {volume:P1}");
                }
                else
                {
                    await e.Channel.SendMessage($"Volume: {musicVolume:P1}" + Environment.NewLine + "Volume must be a number 0-1 (decimal) or 1-100 (percentage)");
                }
            });

        client.ExecuteAndWait(async () =>
        {
            if (File.Exists("CREDENTIALS.txt"))
                await client.Connect(File.ReadAllLines("CREDENTIALS.txt")[0], TokenType.Bot);
        });
    }

    static void Log(object sender, LogMessageEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    static List<string> GetMusicFiles(string searchterm)
    {
        var files = new List<string>();
        if (!string.IsNullOrWhiteSpace(searchterm) && searchterm.Length < 3)
        {
            return new List<string>() { "Search term must be at least 3 characters" };
        }
        if (File.Exists("music/FOLDER_LIST.txt") && !string.IsNullOrWhiteSpace(searchterm))
        {
            var folderList = File.ReadAllLines("music/FOLDER_LIST.txt");
            foreach (var folder in folderList)
            {
                if (Directory.Exists(folder))
                {
                    var shortcutFiles = Directory.GetFiles(folder, $"*{searchterm}*", SearchOption.AllDirectories).Where(name => !name.EndsWith(".txt")).ToArray();
                    files.AddRange(shortcutFiles);
                }
            }
        }
        var folderFiles = Directory.GetFiles("music", $"*{searchterm}*", SearchOption.AllDirectories).Where(name => !name.EndsWith(".txt")).ToArray();
        files.AddRange(folderFiles);
        return files;
    }

    static void PlayMusic(List<string> files, ref DiscordClient client, ref IAudioClient voiceClient, Channel channel)
    {
        var channelCount = client.GetService<AudioService>().Config.Channels;
        var outFormat = new WaveFormat(48000, 16, channelCount);
        foreach (var file in files)
        {
            channel.SendMessage("Now Playing: " + Path.GetFileName(file));
            using (var reader = new MediaFoundationReader(file))
            using (var resampler = new MediaFoundationResampler(reader, outFormat))
            {
                resampler.ResamplerQuality = 60;
                var blockSize = outFormat.AverageBytesPerSecond / 50;
                var buffer = new byte[blockSize];
                int byteCount;

                while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                {
                    if (byteCount < blockSize)
                    {
                        for (var i = byteCount; i < blockSize; i++)
                            buffer[i] = 0;
                    }
                    if (voiceClient.State == ConnectionState.Connected)
                        try
                        {
                            voiceClient.Send(ScaleVolumeSafeNoAlloc(buffer), 0, blockSize);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                }
            }
        }
    }

    public static byte[] ScaleVolumeSafeNoAlloc(byte[] audioSamples)
    {
        var volume = musicVolume;

        if (audioSamples != null && audioSamples.Length % 2 == 0 && volume >= 0f && volume <= 1f)
        {
            if (Math.Abs(volume - 1f) < 0.0001f) return audioSamples;

            int volumeFixed = (int)Math.Round(volume * 65536d);

            for (int i = 0, length = audioSamples.Length; i < length; i += 2)
            {
                int sample = (short)((audioSamples[i + 1] << 8) | audioSamples[i]);
                int processed = (sample * volumeFixed) >> 16;

                audioSamples[i] = (byte)processed;
                audioSamples[i + 1] = (byte)(processed >> 8);
            }
        }
        return audioSamples;
    }

    static string GetImageResultUrl(string query, bool random = false)
    {
        var url = "https://www.google.com/search?q=" + query + "&tbm=isch&safe=active";
        string html;

        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Accept = "text/html, application/xhtml+xml, */*";
        request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64; Trident/7.0; rv:11.0) like Gecko";

        var response = (HttpWebResponse)request.GetResponse();

        using (var dataStream = response.GetResponseStream())
        {
            if (dataStream == null)
                return "";
            using (var sr = new StreamReader(dataStream))
            {
                html = sr.ReadToEnd();
            }
        }

        var urls = new List<string>();

        var ndx = html.IndexOf("\"ou\"", StringComparison.Ordinal);

        while (ndx >= 0)
        {
            ndx = html.IndexOf("\"", ndx + 4, StringComparison.Ordinal);
            ndx++;
            var ndx2 = html.IndexOf("\"", ndx, StringComparison.Ordinal);
            var newUrl = html.Substring(ndx, ndx2 - ndx);
            urls.Add(newUrl);
            ndx = html.IndexOf("\"ou\"", ndx2, StringComparison.Ordinal);
        }

        return random ? urls[new Random().Next(urls.Count)] : urls[0];
    }
}
