using System.Text.Json.Serialization;

class Videos
{
    [JsonPropertyName("items")]
    public List<Video> items { get; set; }
}

class Video : IEquatable<Video>
{
    [JsonPropertyName("id")]
    public string id { get; set; }

    [JsonPropertyName("title")]
    public string title { get; set; }

    [JsonPropertyName("lang")]
    public string lang { get; set; }

    public Video(string id, string title, string lang)
    {
        this.id = id;
        this.title = title;
        this.lang = lang;
    }

    public override string ToString()
    {
        return $"{GetType().Name}( id={id}, title={title}, lang={lang} )";
    }

    public bool Equals(Video? other)
    {
        return other != null && id == other.id;
    }
}