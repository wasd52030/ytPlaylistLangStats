using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using Npgsql;

class UpdateFromPostgresql
{
    public static async Task Invoke(string playListUrl, string apiKey)
    {
        var APIResult = await APICollector.GetPlayListData(playListUrl, apiKey);

        string playListID = APIResult.Item1;
        using JsonDocument playListData = APIResult.Item2;
        JsonElement playListDataRoot = playListData.RootElement;
        var playListDataitems = playListDataRoot.GetProperty("items")[0];

        var ch = playListDataitems.GetProperty("snippet").GetProperty("channelTitle").GetString()!;
        var PlayListTitle = playListDataitems.GetProperty("snippet").GetProperty("localized").GetProperty("title")
            .GetString();

        var jsonPath = $"./resources/{ch}-{PlayListTitle}_videosLangCheck.json";
        string file = await File.ReadAllTextAsync(jsonPath);

        var json = JsonSerializer.Deserialize<Videos>(file);

        var dbResult = await GetVideosAsync(playListID, (ch, PlayListTitle!));

        await File.WriteAllTextAsync(
            $"./resources/{ch}-{PlayListTitle}_videosLangCheck.json",
            JsonSerializer.Serialize(
                new { items = dbResult },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })
        );
    }

    static async Task<IEnumerable<Video>> GetVideosAsync(string playlistId, (string ch, string PlayListTitle) playlistInfo)
    {
        // this connser for local postgres db ONLY!!!!
        var connstr = "Host=localhost;Port=5432;Database=Youtube-Playlist-DB;Username=postgres;Password=password";

        string query = @"
            SELECT v.video_id Id, v.title Title, v.comment comment, v.cover_url CoverUrl, v.lang lang, pl.position Position
            FROM playlist_item pl
            JOIN videos v ON pl.video_id = v.video_id
            JOIN playlists p ON pl.playlist_id = p.playlist_id
            WHERE p.playlist_id = @playlistId
            ORDER BY pl.position;";

        using (var conn = new NpgsqlConnection(connstr))
        {
            conn.Open();
            var playlist = await conn.QueryAsync<Video, PlaylistInfo, Video>(
                query,
                (video, playlist) =>
                {
                    playlist.Id=playlistId;
                    playlist.Owner=playlistInfo.ch;
                    playlist.Title=playlistInfo.PlayListTitle;
                    // Console.WriteLine($"{playlist.Id} {playlist.Owner} {playlist.Title} {playlist.Position}");
                    video.Playlists = new HashSet<PlaylistInfo> { playlist };
                    // Console.WriteLine($"{video.Id} {video.Title} {video.lang} {playlist.Position}");
                    return video;
                },
                new { playlistId },
                splitOn: "position"
            );

            // foreach (var video in playlist)
            // {
            //     Console.WriteLine(video);
            // }

            return playlist;
        }
    }
}