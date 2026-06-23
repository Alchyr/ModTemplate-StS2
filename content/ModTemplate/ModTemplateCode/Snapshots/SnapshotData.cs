namespace ModTemplate.ModTemplateCode.Snapshots;

public class CardData
{
    public string ModelId { get; set; } = "";
    public int UpgradeCount { get; set; }
}

public class RunSnapshot
{
    public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string RunId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int Floor { get; set; }
    public decimal CurrentHp { get; set; }   // game uses decimal
    public decimal MaxHp { get; set; }
    public decimal Gold { get; set; }
    public List<CardData> Deck { get; set; } = [];
    public List<string> RelicIds { get; set; } = [];
    public List<string> PotionIds { get; set; } = [];
}
