using System.CommandLine;
using Microsoft.Extensions.Configuration;

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


    var PlaylistOption = new Option<string>
    (aliases: new string[] { "--playlist", "--pl" },
    description: "youtube playlist url",
    getDefaultValue: () => "https://www.youtube.com/playlist?list=PLdx_s59BrvfXJXyoU5BHpUkZGmZL0g3Ip");

    // download command
    var downloadCommand = new Command(name: "download", description: "初步下載統整播放清單中的語言")
    {
        PlaylistOption
    };
    rootCommand.AddCommand(downloadCommand);

    downloadCommand.SetHandler(async (PlaylistOption) =>
    {
        var apiKey = Environment.GetEnvironmentVariable("YoutubeAPIKey") ?? config.apiKey;
        await CollectData.Invoke(PlaylistOption, apiKey);
    }, PlaylistOption);

    // stat command
    var statCommand = new Command(name: "stat", description: "進行統計")
    {
        PlaylistOption
    };
    rootCommand.AddCommand(statCommand);
    statCommand.SetHandler(async (PlaylistOption) =>
    {
        var apiKey = Environment.GetEnvironmentVariable("YoutubeAPIKey") ?? config.apiKey;
        await DataAnalysis.Invoke(PlaylistOption, apiKey);
    }, PlaylistOption);

    await rootCommand.InvokeAsync(args);
}

await main();