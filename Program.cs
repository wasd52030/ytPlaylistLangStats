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

    var YoutubeAPIKeyOption = new Option<string>(aliases: new string[] { "--ytkey" }, description: "youtube playlist url");

    // download command
    var downloadCommand = new Command(name: "download", description: "初步下載統整播放清單中的語言")
    {
        PlaylistOption,
        YoutubeAPIKeyOption
    };
    rootCommand.AddCommand(downloadCommand);

    downloadCommand.SetHandler(async (PlaylistOption, YoutubeAPIKeyOption) =>
    {
        var apiKey = YoutubeAPIKeyOption ?? config.apiKey;
        await CollectData.Invoke(PlaylistOption, config.apiKey);
    }, PlaylistOption, YoutubeAPIKeyOption);

    // stat command
    var statCommand = new Command(name: "stat", description: "進行統計")
    {
        PlaylistOption,
        YoutubeAPIKeyOption
    };
    rootCommand.AddCommand(statCommand);
    statCommand.SetHandler(async (PlaylistOption, YoutubeAPIKeyOption) =>
    {
        var apiKey = YoutubeAPIKeyOption ?? config.apiKey;
        await DataAnalysis.Invoke(PlaylistOption, apiKey);
    }, PlaylistOption, YoutubeAPIKeyOption);

    await rootCommand.InvokeAsync(args);
}

await main();