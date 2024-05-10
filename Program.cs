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
        await CollectData.GetVideoDetail(playlistOption, config.apiKey);
    }, playlistOption);

    // stat command
    var statCommand = new Command(name: "stat", description: "進行統計，並輸出json檔與sqlite檔")
    {
        playlistOption
    };
    rootCommand.AddCommand(statCommand);
    statCommand.SetHandler(async (playlistOption) =>
    {
        await DataAnalysis.Invoke(playlistOption, config.apiKey);
    }, playlistOption);

    await rootCommand.InvokeAsync(args);
}

await main();