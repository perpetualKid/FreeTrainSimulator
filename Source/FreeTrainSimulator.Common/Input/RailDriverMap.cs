using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using GetText;

namespace FreeTrainSimulator.Common.Input
{
    public static class RailDriverMap
    {
        private static readonly Catalog catalog = CatalogManager.Catalog;

        public static string CheckForErrors(this byte[] buttonSettings)
        {
            StringBuilder errors = new StringBuilder();

            var duplicates = buttonSettings.Where(button => button < 255).
                Select((value, index) => new { Index = index, Button = value }).
                GroupBy(g => g.Button).
                Where(g => g.Count() > 1).
                OrderBy(g => g.Key);

            foreach (var duplicate in duplicates)
            {
                errors.Append(catalog.GetString("Button {0} is assigned to \r\n\t", duplicate.Key));
                foreach (var buttonMapping in duplicate)
                {
                    errors.Append(catalog.GetString($"\"{((UserCommand)buttonMapping.Index).GetLocalizedDescription()}\" and "));
                }
                errors.Remove(errors.Length - 5, 5);
                errors.AppendLine();
            }
            return errors.ToString();
        }

        public static void DumpToText(this EnumArray<byte, UserCommand> userCommands, string filePath)
        {
            var buttonMappings = userCommands.Select((value, index) => new { Index = index, Button = value }).
                Where(button => button.Button < 255).OrderBy(button => button.Button);

            using (StreamWriter writer = new StreamWriter(File.OpenWrite(filePath)))
            {
                writer.WriteLine("{0,-40}{1,-40}", "Command", "Button");
                writer.WriteLine(new string('=', 40 * 2));
                foreach (var buttonMapping in buttonMappings)
                    writer.WriteLine("{0,-40}{1,-40}", ((UserCommand)buttonMapping.Index).GetLocalizedDescription(), buttonMapping.Button);
            }
        }

    }
}
