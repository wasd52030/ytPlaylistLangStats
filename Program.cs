using System;
using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using Dapper;
using MoreLinq;

// return full api result
async Task<string> getPlayListItemData(string url, List<JsonElement> videoList, string apiKey, string pageToken = "", int i = 0)
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
        return await getPlayListItemData(url, videoList, apiKey, token.GetString()!, i);
    }
    else
    {
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.All),
            WriteIndented = true
        };
        // await File.WriteAllTextAsync("./data.json", JsonSerializer.Serialize(new { items = videoList }, options));
        Console.WriteLine("collect PlayListItemData complete");
        return JsonSerializer.Serialize(new { items = videoList }, options);
    }
}

async Task<JsonDocument> getPlayListData(string url, string apiKey)
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

async Task getVideoDetail(string playListURL, string apiKey, string pageToken = "", int i = 0)
{
    List<Video> details = new();

    using JsonDocument playListData = await getPlayListData(playListURL, apiKey);
    JsonElement playListDataRoot = playListData.RootElement;
    var playListDataItems = playListDataRoot.GetProperty("items")[0];

    var ch = playListDataItems.GetProperty("snippet").GetProperty("channelTitle").GetString()!;
    var playListtitle = playListDataItems.GetProperty("snippet").GetProperty("localized").GetProperty("title").GetString()!;

    string playList = await getPlayListItemData(playListURL, new List<JsonElement>(), apiKey, pageToken = "", i = 0);
    using JsonDocument playListItemData = JsonDocument.Parse(playList, new JsonDocumentOptions { AllowTrailingCommas = true });

    JsonElement playListItemDataroot = playListItemData.RootElement;
    JsonElement playListItemDataitems = playListItemDataroot.GetProperty("items");

    JsonSerializerOptions options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true
    };

    IEnumerable<Video>? videDB = null;
    if (File.Exists($"./resources/{ch}-{playListtitle}_videosLangCheck.json"))
    {
        string dbPath = @$"./resources/{ch}-{playListtitle}_videos.sqlite";
        using var db = new SQLiteConnection("data source=" + dbPath);
        videDB = db.Query<Video>("select * from videos");
    }


    foreach (var video in playListItemDataitems.EnumerateArray())
    {
        string? title = video.GetProperty("snippet").GetProperty("title").GetString();
        string? id = video.GetProperty("snippet").GetProperty("resourceId").GetProperty("videoId").GetString();


        HttpClient client = new();
        string url = $"https://youtube.googleapis.com/youtube/v3/videos?part=snippet&id={id}&key={apiKey}";
        var res = await client.GetStringAsync(url);
        using (JsonDocument apiRes = JsonDocument.Parse(res, new JsonDocumentOptions { AllowTrailingCommas = true }))
        {
            JsonElement resRoot = apiRes.RootElement;
            var data = resRoot.GetProperty("items").EnumerateArray().ToArray();
            if (data.Count() == 0)
            {
                Console.WriteLine($"影片id: {id}未找到");
                continue;
            }

            if (videDB != null)
            {
                var exists = videDB.FirstOrDefault(v => v.id == id);
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
                                : "ukunown"
                    );
                details.Add(detail);
            }
        }


        Console.WriteLine($"影片 {title}\nid: {id}\nok！\n");
    }

    await File.WriteAllTextAsync($"./resources/{ch}-{playListtitle}_videosLangCheck.json", JsonSerializer.Serialize(new { items = details }, options));

    Console.Write("因Youtube API能拿到的資料不完整，");
    Console.WriteLine($"請到檔案「{ch}-{playListtitle}_videosLangCheck.json」再次確認每隻影片的語言，再行利用stat指令做統計");
}


async Task maintainDatabase(Videos? json, string dbPath)
{
    using var db = new SQLiteConnection("data source=" + dbPath);
    if (!File.Exists(dbPath))
    {
        db.Execute(@"create table videos (id TEXT PRIMARY KEY,title TEXT,lang TEXT)");
    }

    foreach (var video in json!.items)
    {
        if (video.lang != "ukunown")
        {
            var v = db.Query<Video>("select * from videos where id=@id", new { id = video.id });
            if (!v.Any())
            {
                var insertScript = "INSERT INTO videos VALUES (@id, @title, @lang)";
                var s = db.Execute(insertScript, video);
            }
            else
            {
                var insertScript = "UPDATE videos SET lang=@title, lang=@lang where id=@id";
                var s = db.Execute(insertScript, video);
            }
        }
    }

    // 確保資料庫的東西跟播放清單一致
    var videos = db.Query<Video>("select * from videos");
    var diff = videos.ExceptBy(json.items, video => video.id);
    if (diff.Any())
    {
        foreach (var video in diff)
        {
            Console.WriteLine(video);
            var insertScript = "DELETE FROM videos where id=@id";
            var s = db.Execute(insertScript, new { id = video.id });
        }
    }
}

async Task dataAnalysis(string playListURL, string apiKey)
{
    using JsonDocument playListData = await getPlayListData(playListURL, apiKey);
    JsonElement playListDataRoot = playListData.RootElement;
    var playListDataitems = playListDataRoot.GetProperty("items")[0];

    var ch = playListDataitems.GetProperty("snippet").GetProperty("channelTitle").GetString()!;
    var playListtitle = playListDataitems.GetProperty("snippet").GetProperty("localized").GetProperty("title").GetString()!;

    var jsonPath = $"./resources/{ch}-{playListtitle}_videosLangCheck.json";
    string file = await File.ReadAllTextAsync(jsonPath);

    var json = JsonSerializer.Deserialize<Videos>(file);

    string dbPath = @$"./resources/{ch}-{playListtitle}_videos.sqlite";
    await maintainDatabase(json, dbPath);

    using var db = new SQLiteConnection("data source=" + dbPath);
    var videos = db.Query<Video>("select * from videos");

    var stat = videos.GroupBy(v => v.lang)
                     .OrderByDescending(item => item.Count())
                     .ThenBy(item => item.Key)
                     .ToDictionary(o => o.Key, o => (double)o.Count());
    stat.Add("total", stat.Values.Sum());
    var percent = stat.ToDictionary(record => record.Key, record => record.Value / stat["total"]);

    await File.WriteAllTextAsync(
        Path.Combine(Directory.GetCurrentDirectory(), $"./resources/{ch}-{playListtitle}_result.json"),
        JsonSerializer.Serialize(
            new Dictionary<string, Dictionary<string, double>>() { { "統計", stat }, { "占比", percent } },
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) }
        )
    );
    Console.WriteLine($"playlist {ch}-{playListtitle} stat success！");
}

async Task main()
{
    Console.OutputEncoding = System.Text.Encoding.UTF8;

    IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

    Configure config = new()
    {
        apiKey = configuration.GetValue<string>("YoutubeAPIKey")!,
        isDownloading = configuration.GetValue<bool>("isDownloading")
    };

    // root command
    var rootCommand = new RootCommand("youtube播放清單語言統計");

    // download command
    var downloadCommand = new Command(name: "download", description: "初步下載統整播放清單中的語言");

    var playlistOption = new Option<string>
    (aliases: new string[] { "--playlist", "--pl" },
    description: "youtube playlist url",
    getDefaultValue: () => "https://www.youtube.com/playlist?list=PLdx_s59BrvfXJXyoU5BHpUkZGmZL0g3Ip");

    downloadCommand.AddOption(playlistOption);
    rootCommand.AddCommand(downloadCommand);

    downloadCommand.SetHandler(async (playlistOption) =>
    {
        await getVideoDetail(playlistOption, config.apiKey);
    }, playlistOption);

    // stat command
    var statCommand = new Command(name: "stat", description: "進行統計，並輸出json檔與sqlite檔")
    {
        playlistOption
    };
    rootCommand.AddCommand(statCommand);
    statCommand.SetHandler(async (playlistOption) =>
    {
        await dataAnalysis(playlistOption, config.apiKey);
    }, playlistOption);

    await rootCommand.InvokeAsync(args);
}

await main();