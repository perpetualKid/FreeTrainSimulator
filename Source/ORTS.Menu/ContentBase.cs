using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GNU.Gettext;

namespace ORTS.Menu
{
    public abstract class ContentBase
    {
        protected static GettextResourceManager catalog = new GettextResourceManager("ORTS.Menu");
    }
}
