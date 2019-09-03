public class AllPerfectSplash : AwaitableAnimatedElement
{
    public Game game;
    
    protected override void Awake()
    {
        base.Awake();
        game.onGameCompleted.AddListener(_ => OnGameComplete());
    }

    public void OnGameComplete()
    {
        if (game.State.Score == 1000000)
        {
            game.BeforeExitTasks.Add(Animate());
        }
    }
}