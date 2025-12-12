using System.Text.Encodings.Web;
using System.Text.Json;
using System.Data.SQLite;
using Dapper;
using MoreLinq;

class APICollector
{
    private static readonly HttpClient httpClient = new();

    // return full api result
    private static async Task<string> GetPlayListItemData(string url, List<JsonElement> videoList,
        string apiKey, string pageToken = "", int i = 0)
    {
        Uri playListUrl = new(url);
        var playListUrlArguments = playListUrl.Query
            .Substring(1) // Remove '?'
            .Split('&')
            .Select(q => q.Split('='))
            .ToDictionary(
                q => q.FirstOrDefault()!, // assert that the length is greater than 2
                q => q.Skip(1).FirstOrDefault()!
            );

        UriBuilder apiUrl =
            new(
                $"https://youtube.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=50&playlistId={playListUrlArguments!["list"]}&key={apiKey}");
        if (pageToken != "")
        {
            apiUrl.Query = string.Concat(apiUrl.Query.AsSpan(1), "&", $"pageToken={pageToken}");
        }

        var res = await httpClient.GetStringAsync(apiUrl.Uri);
        using JsonDocument json = JsonDocument.Parse(res, new JsonDocumentOptions { AllowTrailingCommas = true });
        JsonElement root = json.RootElement;
        JsonElement items = root.GetProperty("items");

        foreach (var video in items.EnumerateArray())
        {
            videoList.Add(video);
        }

        if (root.TryGetProperty("nextPageToken", out var token))
        {
            i++;
            Console.WriteLine($"page {i}");
            return await GetPlayListItemData(url, videoList, apiKey, token.GetString()!, i);
        }

        // await File.WriteAllTextAsync("./data.json", JsonSerializer.Serialize(new { items = videoList }, options));
        Console.WriteLine("collect PlayListItemData complete");
        return JsonSerializer.Serialize(
            new { items = videoList },
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
    }

    public static async Task<(string, JsonDocument)> GetPlayListData(string url, string apiKey)
    {
        Uri playListUrl = new(url);
        var playListUrlArguments = playListUrl.Query
            .Substring(1) // Remove '?'
            .Split('&')
            .Select(q => q.Split('='))
            .ToDictionary(
                q => q.FirstOrDefault()!, // assert that the length is greater than 2
                q => q.Skip(1).FirstOrDefault()!
            );

        UriBuilder apiUrl =
            new(
                $"https://youtube.googleapis.com/youtube/v3/playlists?part=snippet&maxResults=50&id={playListUrlArguments!["list"]}&key={apiKey}");

        var res = await httpClient.GetStringAsync(apiUrl.Uri);
        var json = JsonDocument.Parse(res, new JsonDocumentOptions { AllowTrailingCommas = true });
        Console.WriteLine("collect PlayListData complete");
        return (playListUrlArguments!["list"], json);
    }

    public static async Task Invoke(string playListURL, string apiKey, string pageToken = "")
    {
        // List<SQLiteVideo> SQLiteVideos = new();
        List<Video> details = new();

        var playList = await GetPlayListData(playListURL, apiKey);
        using var playListData = playList.Item2;
        JsonElement playListDataRoot = playListData.RootElement;
        var playListDataItems = playListDataRoot.GetProperty("items")[0];

        var plsylistId = playList.Item1;
        var ch = playListDataItems.GetProperty("snippet")
            .GetProperty("channelTitle")
            .GetString()!;
        var playListtitle = playListDataItems.GetProperty("snippet")
            .GetProperty("localized")
            .GetProperty("title")
            .GetString()!;

        string playListItem =
            await GetPlayListItemData(playListURL, new List<JsonElement>(), apiKey, pageToken = "", 0);
        using JsonDocument playListItemData =
            JsonDocument.Parse(playListItem, new JsonDocumentOptions { AllowTrailingCommas = true });

        JsonElement playListItemDataroot = playListItemData.RootElement;
        JsonElement playListItemDataitems = playListItemDataroot.GetProperty("items");

        IEnumerable<Video>? videoDB = null;
        if (File.Exists($"./resources/{ch}-{playListtitle}_videosLangCheck.json"))
        {
            string dbPath = @$"./resources/{ch}-{playListtitle}_videos.sqlite";
            await using var db = new SQLiteConnection("data source=" + dbPath);
            videoDB = db.Query<Video>("select * from videos");
        }

        var PlayListAPI = playListItemDataitems.EnumerateArray().Index();
        foreach (var (i, video) in PlayListAPI)
        {
            int position = video.GetProperty("snippet")
                .GetProperty("position")
                .GetInt32()+1; //拿到的資料從0開始，+1符合習慣

            string? title = video.GetProperty("snippet")
                .GetProperty("title")
                .GetString()!
                .Replace("\n", "");
            string? id = video.GetProperty("snippet")
                .GetProperty("resourceId")
                .GetProperty("videoId")
                .GetString();

            string url = $"https://youtube.googleapis.com/youtube/v3/videos?part=snippet&id={id}&key={apiKey}";
            var res = await httpClient.GetStringAsync(url);
            using (JsonDocument apiRes =
                   JsonDocument.Parse(res, new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                JsonElement resRoot = apiRes.RootElement;
                var data = resRoot.GetProperty("items").EnumerateArray().ToArray();

                // var SQLLiteVideo = new SQLiteVideo(
                //     id!,
                //     title!,
                //     lang
                // );

                if (data.Length == 0)
                {
                    title = $"{title} - Not Found";
                }
                else
                {
                    var lang = data[0]
                        .GetProperty("snippet")
                        .TryGetProperty("defaultAudioLanguage", out var defaultAudioLanguage)
                        ? defaultAudioLanguage.GetString()!
                        : "unknown";
                    Video? currentVideoDetail;
                    
                    if (videoDB != null)
                    {
                        var exists = videoDB.FirstOrDefault(v => v.Id == id);
                        if (exists != null)
                        {
                            currentVideoDetail = exists;
                        }
                        else
                        {
                            currentVideoDetail = new Video(id!, title!, lang, "", "");
                        }
                    }
                    else
                    {
                        currentVideoDetail = new Video(id!, title!, lang, "", "");
                    }
                    
                    currentVideoDetail.Playlists.Add(new PlaylistInfo(plsylistId, ch, playListtitle, position));
                    details.Add(currentVideoDetail);
                }
            }

            // reference -> https://learn.microsoft.com/zh-tw/dotnet/standard/base-types/standard-numeric-format-strings
            Console.WriteLine($"[{i + 1:D4}/{PlayListAPI.Count():D4}] {id} - {title}！");
        }

        await File.WriteAllTextAsync(
            $"./resources/{ch}-{playListtitle}_videosLangCheck.json",
            JsonSerializer.Serialize(
                new { items = details },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })
        );

        Console.Write("因Youtube API能拿到的資料不完整，");
        Console.WriteLine($"請到檔案「{ch}-{playListtitle}_videosLangCheck.json」再次確認每隻影片的語言，再行利用stat指令做統計");
    }
}