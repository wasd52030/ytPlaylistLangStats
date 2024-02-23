using Microsoft.Extensions.Configuration;

class Configure
{
    public string apiKey { get; set; }
    public bool isDownloading { get; set; }

    public override string ToString()
    {
        return $"apikey={apiKey} isDownloading={isDownloading}";
    }
}