using System;
using DG.Tweening;
using Proyecto26;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LeaderboardEntry : ContainerEntry<Leaderboard.Entry>
{
    public GameObject background;
    public Avatar avatar;
    public Text rank;
    public new Text name;
    public Text rating;

    public Leaderboard.Entry Model { get; protected set; }

    private void Awake()
    {
        background.SetActive(false);
    }

    public override void SetModel(Leaderboard.Entry entry)
    {
        Model = entry;
        background.SetActive(entry.owner.Uid == Context.OnlinePlayer.Uid);
        avatar.SetModel(entry.owner);
        rank.text = entry.rank + ".";
        name.text = entry.owner.Uid;
        rating.text = "Rating " + entry.rating.ToString("N2");
    }

    public override Leaderboard.Entry GetModel() => Model;
    
}