﻿using Google;
using Google.Apis.Util;
using Google.Apis.YouTube.v3.Data;
using YoutubeSubVideoManager.Database;

namespace YoutubeSubVideoManager
{
    public static class ChannelMethods
    {
        public static void LoadVideosFromYoutube(this Database.Models.Channel channel)
        {
            using var db = new DatabaseContext();
            var currentChannel = db.Channels.Where(c => c.Id == channel.Id).First();

            if (Program.youtubeService == null)
            {
                throw new InvalidOperationException("youtube service is null");
            }

            Repeatable<string> part = new(["contentDetails", "snippet"]);

            var listChannel = Program.youtubeService.Channels.List(part);
            listChannel.Id = currentChannel.Id;
            var channelResponse = listChannel.Execute();
            var uploadPlaylistId = channelResponse.Items.First().ContentDetails.RelatedPlaylists.Uploads;

            var listRequest = Program.youtubeService.PlaylistItems.List(part);
            listRequest.PlaylistId = uploadPlaylistId;
            listRequest.MaxResults = 1000;
            bool first = true;
            List<Database.Models.Video> videos = new();
            while (true)
            {
                PlaylistItemListResponse response;
                try
                {
                    response = listRequest.Execute();
                }
                catch (GoogleApiException e)
                {
                    if (e.Error.Code == 404)
                    {
                        Console.WriteLine($"Channel {currentChannel.Id}({currentChannel.Title}) has no videos");
                        break;
                    }
                    throw;
                }
                if (first)
                {
                    Console.WriteLine($"Channel {currentChannel.Id}({currentChannel.Title}) has {response.PageInfo.TotalResults} videos");
                    first = false;
                }

                foreach (var video in response.Items)
                {
                    var videoId = video.ContentDetails.VideoId;
                    var videoPublishDate = video.ContentDetails.VideoPublishedAtDateTimeOffset?.UtcDateTime;
                    if (videoPublishDate != null)
                    {
                        var vid = new Database.Models.Video()
                        {
                            Id = videoId,
                            Created = DateTime.Now,
                            Updated = DateTime.Now,
                            PublishDate = videoPublishDate.Value,
                            Channel = currentChannel,
                            Title = video.Snippet.Title,
                        };
                        videos.Add(vid);
                    }
                    //playlists seem to be randomly ordered, so there is no way to know if there arent any more new videos in the list
                }
                if (response.NextPageToken == null)
                {
                    break;
                }
                listRequest.PageToken = response.NextPageToken;
            }
            db.AddRange(videos);
            db.SaveChanges();
        }
    }
}
