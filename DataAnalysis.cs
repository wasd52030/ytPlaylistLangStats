using System.Text.Encodings.Web;
using System.Text.Json;
using System.Data.SQLite;
using Dapper;
using MoreLinq;
using Plotly.NET.ImageExport;
using Plotly.NET;


class DataAnalysis
{
    public static async Task Invoke(string playListUrl, string apiKey)
    {
        using JsonDocument playListData = await CollectData.GetPlayListData(playListUrl, apiKey);
        JsonElement playListDataRoot = playListData.RootElement;
        var playListDataitems = playListDataRoot.GetProperty("items")[0];

        var ch = playListDataitems.GetProperty("snippet").GetProperty("channelTitle").GetString()!;
        var playListtitle = playListDataitems.GetProperty("snippet").GetProperty("localized").GetProperty("title").GetString()!;

        var jsonPath = $"./resources/{ch}-{playListtitle}_videosLangCheck.json";
        string file = await File.ReadAllTextAsync(jsonPath);

        var json = JsonSerializer.Deserialize<Videos>(file);

        string dbPath = @$"./resources/{ch}-{playListtitle}_videos.sqlite";
        MaintainDatabase(json, dbPath);

        using var db = new SQLiteConnection("data source=" + dbPath);
        var videos = db.Query<Video>("select * from videos");

        var baseSeq = videos.GroupBy(v => v.lang)
                         .OrderByDescending(item => item.Count())
                         .ThenBy(item => item.Key)
                         .ToList();

        await MakeJson(baseSeq, ch, playListtitle);
        MakePieChart(baseSeq, ch, playListtitle);
    }

    public static void MaintainDatabase(Videos? json, string dbPath)
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
        var diff = videos.ExceptBy(json.items, video => video.id).ToList();
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

    static async Task MakeJson(List<IGrouping<string, Video>> baseSeq, string ch, string playListtitle)
    {
        var stat = baseSeq.ToDictionary(o => o.Key, o => (double)o.Count());
        stat.Add("total", stat.Values.Sum());
        var percent = stat.ToDictionary(record => record.Key, record => record.Value / stat["total"]);

        await File.WriteAllTextAsync(
            Path.Combine(Directory.GetCurrentDirectory(), $"./resources/{ch}-{playListtitle}_result.json"),
            JsonSerializer.Serialize(
                new Dictionary<string, Dictionary<string, double>>() { { "統計", stat }, { "占比", percent } },
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }
            )
        );
        Console.WriteLine($"playlist {ch}-{playListtitle} stat success！");
    }

    public static void MakePieChart(List<IGrouping<string, Video>> baseSeq, string ch, string playListtitle)
    {
        var plotSeq = baseSeq.Select(item => new { key = item.Key, count = item.Count() })
                             .GroupBy(item => item.count)
                             .Select(item =>
                             {
                                 var s = string.Join(
                                    ", ",
                                    item.Select(item => item.key)
                                       .OrderBy(item => item.Length)
                                       .ThenBy(item => item)
                                       .Take(4)
                                 );

                                 if (item.Count() > 4)
                                 {
                                     s = $"{s}, ... 等{item.Count()}種";
                                 }

                                 var m = item.Count() * item.Key;
                                 return (s, m);
                             })
                             .ToDictionary(item => item.Item1, item => (double)item.Item2);

        var pie = Plotly.NET.CSharp.Chart.Pie<double, string, string>(
            values: plotSeq.Select(item => item.Value).ToList(),
            Labels: plotSeq.Select(item => item.Key).ToList()
        );

        // reference -> https://stackoverflow.com/questions/72504275/make-chart-fill-size-of-browser-window
        pie.WithTitle($"詳細資訊請參考同名的json檔！")
           .WithConfig(Config.init(Responsive: true))
           .SavePNG($"./resources/{ch}-{playListtitle}_result", Width: 1200, Height: 800);
    }
}