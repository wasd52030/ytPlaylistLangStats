using System.Text.Encodings.Web;
using System.Text.Json;
using System.Data.SQLite;
using System.Text.RegularExpressions;
using Dapper;
using MoreLinq;
using Plotly.NET.ImageExport;
using Plotly.NET;


class AnalyticsService
{
    public static async Task Invoke(string playListUrl, string apiKey)
    {
        using JsonDocument playListData = (await APICollector.GetPlayListData(playListUrl, apiKey)).Item2;
        JsonElement playListDataRoot = playListData.RootElement;
        var playListDataitems = playListDataRoot.GetProperty("items")[0];

        var ch = playListDataitems.GetProperty("snippet").GetProperty("channelTitle").GetString()!;
        var playListtitle = playListDataitems.GetProperty("snippet").GetProperty("localized").GetProperty("title")
            .GetString()!;

        var jsonPath = $"./resources/{ch}-{playListtitle}_videosLangCheck.json";
        string file = await File.ReadAllTextAsync(jsonPath);

        var json = JsonSerializer.Deserialize<Videos>(file);

        string dbPath = @$"./resources/{ch}-{playListtitle}_videos.sqlite";
        MaintainSqlite(json, dbPath);

        using var db = new SQLiteConnection("data source=" + dbPath);
        var videos = db.Query<Video>("select * from videos");

        string pattern = @"\[(.*?)\]";

        var base_seq = videos
            .Where(video => !string.IsNullOrEmpty(video.lang))
            .SelectMany(video =>
            {
                var lang = video.lang.Trim();
                if (lang.Contains('[') && lang.Contains(']'))
                {
                    return Regex.Matches(video.lang, pattern)
                        .Select(m => m.Groups[1].Value.Trim())
                        .Where(lang => !string.IsNullOrEmpty(lang))
                        .Select(lang => new { Lang = lang, Video = video });
                }

                return new[] { new { Lang = lang, Video = video } };
            })
            .GroupBy(t => t.Lang, t => t.Video)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .ToList();

        // var baseSeq = videos.GroupBy(v => v.lang)
        //     .OrderByDescending(item => item.Count())
        //     .ThenBy(item => item.Key)
        //     .ToList();

        await MakeJson(base_seq, ch, playListtitle);
        MakePieChart(base_seq, ch, playListtitle);
    }

    static void MaintainSqlite(Videos? json, string dbPath)
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
                var sqlite_db = db.Query<Video>("select * from videos where id=@id", new { id = video.Id });
                var v = new { id = video.Id, video.lang, title = video.Title };
                if (!sqlite_db.Any())
                {
                    var insertScript = "INSERT INTO videos VALUES (@id, @title, @lang)";
                    var s = db.Execute(insertScript, v);
                }
                else
                {
                    var insertScript = "UPDATE videos SET title=@title, lang=@lang where id=@id";
                    var s = db.Execute(insertScript, v);
                }
            }
        }

        // 確保資料庫的東西跟播放清單一致
        var videos = db.Query<Video>("select * from videos");
        var diff = videos.ExceptBy(json.items, video => video.Id).ToList();
        if (diff.Any())
        {
            foreach (var video in diff)
            {
                Console.WriteLine(video);
                var insertScript = "DELETE FROM videos where id=@id";
                var s = db.Execute(insertScript, new { id = video.Id });
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

    static void MakePieChart(List<IGrouping<string, Video>> baseSeq, string ch, string playListtitle)
    {
        var total = baseSeq.Aggregate(0d, (past, curr) => past + curr.Count());

        var plotSeq = baseSeq.Select(item =>
            {
                var s = item.Key;

                if (item.Count() / total < 0.05)
                {
                    s = "others";
                }

                var m = item.Count() * item.Count();
                return (s, item.Count());
            })
            .GroupBy(item => item.s)
            .Select(item => (item.Key, item.Sum(item => item.Item2)))
            .ToDictionary(item => item.Key, item => (double)item.Item2);

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