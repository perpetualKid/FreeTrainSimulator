
using Orts.ActivityRunner.Processes;

namespace Orts.ActivityRunner.Viewer3D.Materials
{
    internal sealed class LoadingBarMaterial : LoadingMaterial
    {
        public LoadingBarMaterial(GameHost game)
            : base(game)
        {
        }

        public override void SetState(Material previousMaterial)
        {
            base.SetState(previousMaterial);
            Shader.CurrentTechnique = Shader.Techniques[1]; //["LoadingBar"];
        }
    }
}
