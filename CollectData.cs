using System.Text.Encodings.Web;
using System.Text.Json;
using System.Data.SQLite;
using Dapper;
using MoreLinq;

class CollectData
{
    // return full api result
    public static async Task<string> GetPlayListItemData(string url, List<JsonElement> videoList, string apiKey, string pageToken = "", int i = 0)
    {
        Uri playListUrl = new(url);
        var playListUrlArguments = playListUrl.Query
                                              .Substring(1) // Remove '?'
                                              .Split('&')
                                              .Select(q => q.Split('='))
                                              .ToDictionary(
                                                q => q.FirstOrDefault()!,       // assert that the length is greater than 2
                                                q => q.Skip(1).FirstOrDefault()!
                                              );

        UriBuilder apiUrl = new($"https://youtube.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=50&playlistId={playListUrlArguments!["list"]}&key={apiKey}");
        if (pageToken != "")
        {
            apiUrl.Query = string.Concat(apiUrl.Query.AsSpan(1), "&", $"pageToken={pageToken}");
        }

        HttpClient client = new();
        var res = await client.GetStringAsync(apiUrl.Uri);
        using JsonDocument json = JsonDocument.Parse(res, new JsonDocumentOptions { AllowTrailingCommas = true });
        JsonElement root = json.RootElement;
        JsonElement items = root.GetProperty("items");

        foreach (var video in items.EnumerateArray())
        {
            videoList.Add(video);
        }

        if (root.TryGetProperty("nextPageToken", out JsonElement token))
        {
            i++;
            Console.WriteLine($"page {i}");
            return await GetPlayListItemData(url, videoList, apiKey, token.GetString()!, i);
        }
        else
        {
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
    }

    public static async Task<JsonDocument> GetPlayListData(string url, string apiKey)
    {
        Uri playListUrl = new(url);
        var playListUrlArguments = playListUrl.Query
                                              .Substring(1) // Remove '?'
                                              .Split('&')
                                              .Select(q => q.Split('='))
                                              .ToDictionary(
                                                q => q.FirstOrDefault()!,       // assert that the length is greater than 2
                                                q => q.Skip(1).FirstOrDefault()!
                                              );
        
        UriBuilder apiUrl = new($"https://youtube.googleapis.com/youtube/v3/playlists?part=snippet&maxResults=50&id={playListUrlArguments!["list"]}&key={apiKey}");

        HttpClient client = new();
        var res = await client.GetStringAsync(apiUrl.Uri);
        var json = JsonDocument.Parse(res, new JsonDocumentOptions { AllowTrailingCommas = true });

        Console.WriteLine("collect PlayListData complete");
        return json;
    }

    public static async Task Invoke(string playListURL, string apiKey, string pageToken = "")
    {
        List<Video> details = new();

        using JsonDocument playListData = await GetPlayListData(playListURL, apiKey);
        JsonElement playListDataRoot = playListData.RootElement;
        var playListDataItems = playListDataRoot.GetProperty("items")[0];

        var ch = playListDataItems.GetProperty("snippet")
                                  .GetProperty("channelTitle")
                                  .GetString()!;
        var playListtitle = playListDataItems.GetProperty("snippet")
                                             .GetProperty("localized")
                                             .GetProperty("title")
                                             .GetString()!;

        string playList = await GetPlayListItemData(playListURL, new List<JsonElement>(), apiKey, pageToken = "", 0);
        using JsonDocument playListItemData = JsonDocument.Parse(playList, new JsonDocumentOptions { AllowTrailingCommas = true });

        JsonElement playListItemDataroot = playListItemData.RootElement;
        JsonElement playListItemDataitems = playListItemDataroot.GetProperty("items");

        IEnumerable<Video>? videoDB = null;
        if (File.Exists($"./resources/{ch}-{playListtitle}_videosLangCheck.json"))
        {
            string dbPath = @$"./resources/{ch}-{playListtitle}_videos.sqlite";
            using var db = new SQLiteConnection("data source=" + dbPath);
            videoDB = db.Query<Video>("select * from videos");
        }

        var v = playListItemDataitems.EnumerateArray().Select((value, i) => (i, value));
        foreach (var (i, video) in v)
        {
            string? title = video.GetProperty("snippet")
                                 .GetProperty("title")
                                 .GetString()!
                                 .Replace("\n", "");
            string? id = video.GetProperty("snippet")
                              .GetProperty("resourceId")
                              .GetProperty("videoId")
                              .GetString();


            HttpClient client = new();
            string url = $"https://youtube.googleapis.com/youtube/v3/videos?part=snippet&id={id}&key={apiKey}";
            var res = await client.GetStringAsync(url);
            using (JsonDocument apiRes = JsonDocument.Parse(res, new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                JsonElement resRoot = apiRes.RootElement;
                var data = resRoot.GetProperty("items").EnumerateArray().ToArray();
                if (data.Count() == 0)
                {
                    title = "No Found";
                    continue;
                }

                if (videoDB != null)
                {
                    var exists = videoDB.FirstOrDefault(v => v.id == id);
                    if (exists != null)
                    {
                        details.Add(exists);
                    }
                    else
                    {
                        var detail = new Video(
                            id!,
                            title!,
                            data[0].GetProperty("snippet").TryGetProperty("defaultAudioLanguage", out JsonElement lang)
                                    ? lang.GetString()!
                                    : "unknown"
                        );
                        details.Add(detail);
                    }
                }
                else
                {
                    var detail = new Video(
                            id!,
                            title!,
                            data[0].GetProperty("snippet").TryGetProperty("defaultAudioLanguage", out JsonElement lang)
                                    ? lang.GetString()!
                                    : "unknown"
                        );
                    details.Add(detail);
                }
            }

            Console.WriteLine($"[{i + 1}/{v.Count()}] {id} - {title}！");
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