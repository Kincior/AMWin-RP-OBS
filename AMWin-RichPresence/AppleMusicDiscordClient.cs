﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AMWin_RichPresence;
using DiscordRPC;

internal class AppleMusicDiscordClient {
    public enum RPSubtitleDisplayOptions {
        ArtistAlbum = 0, ArtistOnly = 1, AlbumOnly = 2
    }

    public static RPSubtitleDisplayOptions SubtitleOptionFromIndex(int i) {
        return (RPSubtitleDisplayOptions)i;
    }

    public RPSubtitleDisplayOptions subtitleOptions;
    DiscordRpcClient? client;
    string discordClientID;
    bool enabled = false;

    int maxStringLength = 127;

    public AppleMusicDiscordClient(string discordClientID, bool enabled = true, RPSubtitleDisplayOptions subtitleOptions = RPSubtitleDisplayOptions.ArtistAlbum) {
        this.discordClientID = discordClientID;
        this.enabled = enabled;
        this.subtitleOptions = subtitleOptions;

        if (enabled) {
            InitClient();
        }
    }

    private string TrimString(string str) {
        return str.Length > maxStringLength ? str.Substring(0, maxStringLength - 1) : str;
    }

    private string GetTrimmedArtistList(AppleMusicInfo amInfo) {
        if (amInfo.ArtistList?.Count > 1) {
            return $"{amInfo.ArtistList.First()}, Various Artists";
        } else {
            return amInfo.SongArtist; // TODO fix this so it always prevents string length violations
        }
    }

    public void SetPresence(AppleMusicInfo amInfo, bool showSmallImage) {
        if (!enabled) {
            return;
        }

        var songName = TrimString(amInfo.SongName);
        var songSubtitle = amInfo.SongSubTitle.Length > maxStringLength ? amInfo.SongSubTitle.Replace(amInfo.SongArtist, GetTrimmedArtistList(amInfo)) : amInfo.SongSubTitle;
        var songArtist = GetTrimmedArtistList(amInfo);
        var songAlbum = TrimString(amInfo.SongAlbum);

        // pick the subtitle format to show
        var subtitle = "";
        switch (subtitleOptions) {
            case RPSubtitleDisplayOptions.ArtistAlbum:
                subtitle = songSubtitle;
                break;
            case RPSubtitleDisplayOptions.ArtistOnly:
                subtitle = songArtist;
                break;
            case RPSubtitleDisplayOptions.AlbumOnly:
                subtitle = songAlbum;
                break;
        }
        try {
            var rp = new RichPresence() {
                Details = songName,
                State = subtitle,
                Assets = new Assets() {
                    LargeImageKey = amInfo.CoverArtUrl ?? Constants.DiscordAppleMusicImageKey,
                    LargeImageText = songName
                }
            };

            if (showSmallImage) {
                rp.Assets.SmallImageKey = (amInfo.CoverArtUrl == null) ? Constants.DiscordAppleMusicPlayImageKey : Constants.DiscordAppleMusicImageKey;
            }

            // add timestamps, if they're there
            if (!amInfo.IsPaused && amInfo.PlaybackStart != null && amInfo.PlaybackEnd != null) {
                rp = rp.WithTimestamps(new Timestamps((DateTime)amInfo.PlaybackStart, (DateTime)amInfo.PlaybackEnd));
            }

            client?.SetPresence(rp);

            Trace.WriteLine($"Set Discord RP to:\n{amInfo}\n");
            string documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string songInfoFolderPath = Path.Combine(documentsDirectory, "SongInfo");
            string songInfoFilePath = Path.Combine(songInfoFolderPath, "song_info.txt");
            string songInfo = $"{amInfo.SongName} by {amInfo.SongArtist}\n";
            File.WriteAllText(songInfoFilePath, songInfo);

        } catch (Exception ex) {
            Trace.WriteLine($"Couldn't set Discord RP:\n{ex}");
        }

    }
    public void Enable() {
        if (enabled) {
            return;
        }
        enabled = true;
        InitClient();
    }
    public void Disable() {
        if (!enabled) {
            return;
        }
        enabled = false;
        client?.ClearPresence();
        DeinitClient();
    }
    private void InitClient() {
        client = new DiscordRpcClient(discordClientID);
        client.Initialize();
    }
    private void DeinitClient() {
        if (client != null) {
            client.Deinitialize();
            client = null;
        }
    }
}