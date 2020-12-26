using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

using Orts.Common;

namespace Orts.TrackEditor
{
    public partial class GameWindow : Game
    {
        public void ChangeScreenMode()
        {
            SynchronizeGraphicsDeviceManager(currentScreenMode.Next());
        }

        public void CloseWindow()
        {
            if (System.Windows.Forms.MessageBox.Show("Title", "Text", MessageBoxButtons.OKCancel) == DialogResult.OK)
                windowForm.Close();
        }
    }
}
