using System.Text.Json.Serialization;

public class Videos
{
    [JsonPropertyName("items")] public HashSet<Video> items { get; set; }
}

public class PlaylistInfo : IEquatable<PlaylistInfo>
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("owner")] public string Owner { get; set; }

    [JsonPropertyName("title")] public string Title { get; set; }

    [JsonPropertyName("position")] public int Position { get; set; }

    public PlaylistInfo(string id, string owner, string title, int position)
    {
        Id = id;
        Owner = owner;
        Title = title;
        Position = position;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if ((obj == null) || !(obj is PlaylistInfo)) return false;
        return base.Equals(obj);
    }

    public bool Equals(PlaylistInfo? other)
    {
        if (other == null) return false;
        return Id == other.Id;
    }
}


public class Video : IEquatable<Video>
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("title")] public string Title { get; set; }


    [JsonPropertyName("comment")] public string? comment { get; set; }

    [JsonPropertyName("CoverUrl")] public string CoverUrl { get; set; }

    [JsonPropertyName("lang")] public string lang { get; set; }

    [JsonPropertyName("playlists")] public HashSet<PlaylistInfo> Playlists { get; set; }

    public Video()
    {
        Id = "";
        Title = "空的kora";
        lang = "";
        comment = "";
        Playlists = new HashSet<PlaylistInfo>();
        CoverUrl = "";
    }

    public Video(string id, string title, string lang, string? comment, string coverUrl)
    {
        Id = id;
        Title = title;
        this.lang = lang;
        this.comment = comment;
        Playlists = new HashSet<PlaylistInfo>();
        CoverUrl = coverUrl;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public override string ToString()
    {
        return $"{GetType().Name}(id={Id}, name={Title}, description={comment})";
    }

    public override bool Equals(object? obj)
    {
        if ((obj == null) || !(obj is Video)) return false;
        return base.Equals(obj);
    }

    public bool Equals(Video? other)
    {
        return Id == other.Id;
    }
}