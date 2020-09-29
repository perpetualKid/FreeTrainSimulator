
using Orts.ActivityRunner.Viewer3D.Processes;

namespace Orts.ActivityRunner.Viewer3D.Materials
{
    internal class LoadingBarMaterial : LoadingMaterial
    {
        public LoadingBarMaterial(Game game)
            : base(game)
        {
        }

        public override void SetState(Material previousMaterial)
        {
            base.SetState(previousMaterial);
            shader.CurrentTechnique = shader.Techniques[1]; //["LoadingBar"];
        }
    }
}
