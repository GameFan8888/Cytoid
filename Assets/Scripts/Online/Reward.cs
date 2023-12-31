using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[Serializable]
public class Reward
{
    public string type;
    public JObject value;
    public Lazy<OnlineLevel> onlineLevelValue;
    public Lazy<CharacterMeta> characterValue;
    public Lazy<Badge> badgeValue;

    public RewardType Type => (RewardType) Enum.Parse(typeof(RewardType), type, true);

    [JsonConstructor]
    public Reward()
    {
        onlineLevelValue = new Lazy<OnlineLevel>(() => value.ToObject<OnlineLevel>());
        characterValue = new Lazy<CharacterMeta>(() => value.ToObject<CharacterMeta>());
        badgeValue = new Lazy<Badge>(() => value.ToObject<Badge>());
    }
        
    public enum RewardType
    {
        Character, Level, Badge
    }
}