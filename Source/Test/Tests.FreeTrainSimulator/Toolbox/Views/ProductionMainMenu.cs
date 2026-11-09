using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;

namespace Tests.FreeTrainSimulator.Toolbox.Views
{
    internal static class ProductionMainMenu
    {
        internal static Menu Load()
        {
            using (Stream stream = typeof(ProductionMainMenu).Assembly.GetManifestResourceStream("Tests.FreeTrainSimulator.Toolbox.MainWindow.xaml")
                ?? throw new InvalidOperationException("The production MainWindow XAML resource is missing."))
            {
                XDocument document = XDocument.Load(stream);
                XNamespace presentation = document.Root.Name.Namespace;
                XNamespace xaml = document.Root.GetNamespaceOfPrefix("x");
                XElement menu = new XElement(document.Descendants(presentation + "Menu")
                    .Single(element => (string)element.Attribute(xaml + "Name") == "MainMenu"));
                // Load only the production menu: the full shell constructs the GPU host.
                foreach (XAttribute declaration in document.Root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration))
                {
                    if (menu.Attribute(declaration.Name) == null)
                        menu.Add(new XAttribute(declaration));
                }
                return (Menu)XamlReader.Parse(menu.ToString());
            }
        }

        internal static MenuItem FindCloseRouteItem(Menu menu)
        {
            MenuItem file = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == "_File");
            return file.Items.OfType<MenuItem>().Single(item => item.Name == "CloseRouteMenuItem");
        }
    }
}
