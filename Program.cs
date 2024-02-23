using System;
using System.CommandLine;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Configuration;
using System.Data.SQLite;
using Dapper;
using MoreLinq;

async Task getPlayListData(string url, List<JsonElement> videoList, string apiKey, string pageToken = "", int i = 0)
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
        Console.WriteLine(i);
        await getPlayListData(url, videoList, apiKey, token.GetString()!, i);
    }
    else
    {
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true
        };
        await File.WriteAllTextAsync("./data.json", JsonSerializer.Serialize(new { items = videoList }, options));
    }
}

// TODO: 檢查是否有已存在的result.json，有的話以原有檔案為基準增加
async Task getVideoDetail(string path, string apiKey)
{
    string playList = await File.ReadAllTextAsync(path);
    List<Video> details = new();

    using JsonDocument playListData = JsonDocument.Parse(playList, new JsonDocumentOptions { AllowTrailingCommas = true });

    JsonElement root = playListData.RootElement;
    JsonElement items = root.GetProperty("items");

    JsonSerializerOptions options = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true
    };

    string dbPath = @"./videos.sqlite";
    using var db = new SQLiteConnection("data source=" + dbPath);
    var videos = db.Query<Video>("select * from videos");

    foreach (var video in items.EnumerateArray())
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

            var exists = videos.FirstOrDefault(v => v.id == id);
            if (exists != null)
            {
                Console.WriteLine(6);
                details.Add(exists);
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

        Console.WriteLine($"影片 {title};id: {id} ok！");
    }

    await File.WriteAllTextAsync("./videos_temp.json", JsonSerializer.Serialize(new { items = details }, options));
}

async Task maintainDatabase(string path)
{
    string file = await File.ReadAllTextAsync(path);

    var json = JsonSerializer.Deserialize<Videos>(file);

    string dbPath = @"./videos.sqlite";
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
        }
    }

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

async Task dataAnalysis(string path)
{
    await maintainDatabase(path);

    string dbPath = @"./videos.sqlite";
    using var db = new SQLiteConnection("data source=" + dbPath);
    var videos = db.Query<Video>("select * from videos");

    var stat = videos.GroupBy(v => v.lang).ToDictionary(o => o.Key, o => (double)o.Count());

    stat = stat.OrderByDescending(l => l.Value).ToDictionary(l => l.Key, l => l.Value);
    stat.Add("total", stat.Values.Sum());

    var percent = stat.ToDictionary(record => record.Key, record => record.Value / stat["total"]);

    await File.WriteAllTextAsync(
        Path.Combine(Directory.GetCurrentDirectory(), "result.json"),
        JsonSerializer.Serialize(
            new Dictionary<string, Dictionary<string, double>>() { { "統計", stat }, { "占比", percent } },
            new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) }
        )
    );
    Console.WriteLine("stat success！");
}

async Task main()
{

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
    var downloadCommand = new Command(name: "download", description: "初步下載統整播放清單中的語言,存入viedos.json");
    rootCommand.AddCommand(downloadCommand);
    downloadCommand.SetHandler(async () =>
    {
        await getPlayListData(
            "https://www.youtube.com/playlist?list=PLdx_s59BrvfXJXyoU5BHpUkZGmZL0g3Ip",
            new List<JsonElement>(),
            config.apiKey
        );
        await getVideoDetail("./data.json", config.apiKey!);
        Console.Write("因Youtube API能拿到的資料不完整，");
        Console.WriteLine("請再次確認每隻影片的語言，再行利用stat指令做統計");
    });

    // stat command
    var statCommand = new Command(name: "stat", description: "計算viedos.json中的語言種類與各有幾支影片，輸出到result.json");
    rootCommand.AddCommand(statCommand);
    statCommand.SetHandler(async () =>
    {
        await dataAnalysis("./videos_temp.json");
    });

    await rootCommand.InvokeAsync(args);
}

await main();