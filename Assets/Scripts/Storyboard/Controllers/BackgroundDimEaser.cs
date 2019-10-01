namespace Cytoid.Storyboard.Controllers
{
    public class BackgroundDimEaser : StoryboardRendererEaser<ControllerState>
    {
        public override void OnUpdate()
        {
            if (From.BackgroundDim.IsSet())
            {
                Provider.Cover.color = Provider.Cover.color.WithAlpha(EaseFloat(1 - From.BackgroundDim, 1 - To.BackgroundDim));
            }
        }
    }
}