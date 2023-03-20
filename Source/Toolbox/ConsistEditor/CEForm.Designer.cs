using System.Drawing;
using System.Windows.Forms;

namespace ConsistEditor
{
    partial class CEForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            CEmenuStrip = new MenuStrip();
            FileToolStripMenuItem = new ToolStripMenuItem();
            NewToolStripMenuItem = new ToolStripMenuItem();
            SaveToolStripMenuItem = new ToolStripMenuItem();
            ExitToolStripMenuItem = new ToolStripMenuItem();
            consistToolStripMenuItem = new ToolStripMenuItem();
            reverseToolStripMenuItem = new ToolStripMenuItem();
            cloneToolStripMenuItem = new ToolStripMenuItem();
            deleteToolStripMenuItem = new ToolStripMenuItem();
            openInExternalEditorToolStripMenuItem = new ToolStripMenuItem();
            saveAsEngSetToolStripMenuItem = new ToolStripMenuItem();
            engToolStripMenuItem = new ToolStripMenuItem();
            findConsistsToolStripMenuItem = new ToolStripMenuItem();
            openInExternalEditorToolStripMenuItem1 = new ToolStripMenuItem();
            openLegacyENGInToolStripMenuItem = new ToolStripMenuItem();
            reloadEngToolStripMenuItem = new ToolStripMenuItem();
            replaceToolStripMenuItem = new ToolStripMenuItem();
            onlySelectedUnitToolStripMenuItem = new ToolStripMenuItem();
            allUnitsInSelectedConsistsToolStripMenuItem = new ToolStripMenuItem();
            allUnitsInAllConsistsToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            consistsListsToolStripMenuItem = new ToolStripMenuItem();
            engList1ToolStripMenuItem = new ToolStripMenuItem();
            engList2ToolStripMenuItem = new ToolStripMenuItem();
            consistsUnitsToolStripMenuItem = new ToolStripMenuItem();
            engViewToolStripMenuItem = new ToolStripMenuItem();
            conViewToolStripMenuItem = new ToolStripMenuItem();
            dViewToolStripMenuItem = new ToolStripMenuItem();
            shapeViewResetToolStripMenuItem = new ToolStripMenuItem();
            shapeViewCopyImageToolStripMenuItem = new ToolStripMenuItem();
            shapeViewSaveImageToolStripMenuItem = new ToolStripMenuItem();
            shapeViewSetColorToolStripMenuItem = new ToolStripMenuItem();
            conViewSetColorToolStripMenuItem = new ToolStripMenuItem();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            autoLoadENGSetsToolStripMenuItem = new ToolStripMenuItem();
            refreshEngDataToolStripMenuItem = new ToolStripMenuItem();
            forceReloadEngDataToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            listBox1 = new ListBox();
            listBox2 = new ListBox();
            CEmenuStrip.SuspendLayout();
            SuspendLayout();
            // 
            // CEmenuStrip
            // 
            CEmenuStrip.BackColor = Color.Gray;
            CEmenuStrip.Items.AddRange(new ToolStripItem[] { FileToolStripMenuItem, consistToolStripMenuItem, engToolStripMenuItem, replaceToolStripMenuItem, viewToolStripMenuItem, dViewToolStripMenuItem, settingsToolStripMenuItem, helpToolStripMenuItem });
            CEmenuStrip.Location = new Point(0, 0);
            CEmenuStrip.Name = "CEmenuStrip";
            CEmenuStrip.Size = new Size(1184, 24);
            CEmenuStrip.TabIndex = 0;
            CEmenuStrip.Text = "menuStrip1";
            // 
            // FileToolStripMenuItem
            // 
            FileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { NewToolStripMenuItem, SaveToolStripMenuItem, ExitToolStripMenuItem });
            FileToolStripMenuItem.Name = "FileToolStripMenuItem";
            FileToolStripMenuItem.ShortcutKeyDisplayString = "";
            FileToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.F;
            FileToolStripMenuItem.Size = new Size(37, 20);
            FileToolStripMenuItem.Text = "File";
            // 
            // NewToolStripMenuItem
            // 
            NewToolStripMenuItem.Name = "NewToolStripMenuItem";
            NewToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.N;
            NewToolStripMenuItem.Size = new Size(137, 22);
            NewToolStripMenuItem.Text = "New";
            // 
            // SaveToolStripMenuItem
            // 
            SaveToolStripMenuItem.Name = "SaveToolStripMenuItem";
            SaveToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.S;
            SaveToolStripMenuItem.Size = new Size(137, 22);
            SaveToolStripMenuItem.Text = "Save";
            // 
            // ExitToolStripMenuItem
            // 
            ExitToolStripMenuItem.Name = "ExitToolStripMenuItem";
            ExitToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.X;
            ExitToolStripMenuItem.Size = new Size(137, 22);
            ExitToolStripMenuItem.Text = "Exit";
            ExitToolStripMenuItem.Click += ExitToolStripMenuItem_Click;
            // 
            // consistToolStripMenuItem
            // 
            consistToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { reverseToolStripMenuItem, cloneToolStripMenuItem, deleteToolStripMenuItem, openInExternalEditorToolStripMenuItem, saveAsEngSetToolStripMenuItem });
            consistToolStripMenuItem.Name = "consistToolStripMenuItem";
            consistToolStripMenuItem.Size = new Size(58, 20);
            consistToolStripMenuItem.Text = "Consist";
            // 
            // reverseToolStripMenuItem
            // 
            reverseToolStripMenuItem.Name = "reverseToolStripMenuItem";
            reverseToolStripMenuItem.Size = new Size(195, 22);
            reverseToolStripMenuItem.Text = "Reverse";
            // 
            // cloneToolStripMenuItem
            // 
            cloneToolStripMenuItem.Name = "cloneToolStripMenuItem";
            cloneToolStripMenuItem.Size = new Size(195, 22);
            cloneToolStripMenuItem.Text = "Clone";
            // 
            // deleteToolStripMenuItem
            // 
            deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            deleteToolStripMenuItem.Size = new Size(195, 22);
            deleteToolStripMenuItem.Text = "Delete";
            // 
            // openInExternalEditorToolStripMenuItem
            // 
            openInExternalEditorToolStripMenuItem.Name = "openInExternalEditorToolStripMenuItem";
            openInExternalEditorToolStripMenuItem.Size = new Size(195, 22);
            openInExternalEditorToolStripMenuItem.Text = "Open in external editor";
            // 
            // saveAsEngSetToolStripMenuItem
            // 
            saveAsEngSetToolStripMenuItem.Name = "saveAsEngSetToolStripMenuItem";
            saveAsEngSetToolStripMenuItem.Size = new Size(195, 22);
            saveAsEngSetToolStripMenuItem.Text = "Save as Eng Set";
            // 
            // engToolStripMenuItem
            // 
            engToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { findConsistsToolStripMenuItem, openInExternalEditorToolStripMenuItem1, openLegacyENGInToolStripMenuItem, reloadEngToolStripMenuItem });
            engToolStripMenuItem.Name = "engToolStripMenuItem";
            engToolStripMenuItem.Size = new Size(39, 20);
            engToolStripMenuItem.Text = "Eng";
            // 
            // findConsistsToolStripMenuItem
            // 
            findConsistsToolStripMenuItem.Name = "findConsistsToolStripMenuItem";
            findConsistsToolStripMenuItem.Size = new Size(235, 22);
            findConsistsToolStripMenuItem.Text = "Find Consists";
            // 
            // openInExternalEditorToolStripMenuItem1
            // 
            openInExternalEditorToolStripMenuItem1.Name = "openInExternalEditorToolStripMenuItem1";
            openInExternalEditorToolStripMenuItem1.Size = new Size(235, 22);
            openInExternalEditorToolStripMenuItem1.Text = "Open in external editor";
            // 
            // openLegacyENGInToolStripMenuItem
            // 
            openLegacyENGInToolStripMenuItem.Name = "openLegacyENGInToolStripMenuItem";
            openLegacyENGInToolStripMenuItem.Size = new Size(235, 22);
            openLegacyENGInToolStripMenuItem.Text = "Open legacy ENG in ext. editor";
            // 
            // reloadEngToolStripMenuItem
            // 
            reloadEngToolStripMenuItem.Name = "reloadEngToolStripMenuItem";
            reloadEngToolStripMenuItem.Size = new Size(235, 22);
            reloadEngToolStripMenuItem.Text = "Reload Shape";
            // 
            // replaceToolStripMenuItem
            // 
            replaceToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { onlySelectedUnitToolStripMenuItem, allUnitsInSelectedConsistsToolStripMenuItem, allUnitsInAllConsistsToolStripMenuItem });
            replaceToolStripMenuItem.Name = "replaceToolStripMenuItem";
            replaceToolStripMenuItem.Size = new Size(60, 20);
            replaceToolStripMenuItem.Text = "Replace";
            // 
            // onlySelectedUnitToolStripMenuItem
            // 
            onlySelectedUnitToolStripMenuItem.Name = "onlySelectedUnitToolStripMenuItem";
            onlySelectedUnitToolStripMenuItem.Size = new Size(224, 22);
            onlySelectedUnitToolStripMenuItem.Text = "Only selected Unit";
            // 
            // allUnitsInSelectedConsistsToolStripMenuItem
            // 
            allUnitsInSelectedConsistsToolStripMenuItem.Name = "allUnitsInSelectedConsistsToolStripMenuItem";
            allUnitsInSelectedConsistsToolStripMenuItem.Size = new Size(224, 22);
            allUnitsInSelectedConsistsToolStripMenuItem.Text = "All Units in selected Consists";
            // 
            // allUnitsInAllConsistsToolStripMenuItem
            // 
            allUnitsInAllConsistsToolStripMenuItem.Name = "allUnitsInAllConsistsToolStripMenuItem";
            allUnitsInAllConsistsToolStripMenuItem.Size = new Size(224, 22);
            allUnitsInAllConsistsToolStripMenuItem.Text = "All Units in All Consists";
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { consistsListsToolStripMenuItem, engList1ToolStripMenuItem, engList2ToolStripMenuItem, consistsUnitsToolStripMenuItem, engViewToolStripMenuItem, conViewToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // consistsListsToolStripMenuItem
            // 
            consistsListsToolStripMenuItem.Name = "consistsListsToolStripMenuItem";
            consistsListsToolStripMenuItem.Size = new Size(148, 22);
            consistsListsToolStripMenuItem.Text = "Consists Lists";
            // 
            // engList1ToolStripMenuItem
            // 
            engList1ToolStripMenuItem.Name = "engList1ToolStripMenuItem";
            engList1ToolStripMenuItem.Size = new Size(148, 22);
            engList1ToolStripMenuItem.Text = "Eng List 1";
            // 
            // engList2ToolStripMenuItem
            // 
            engList2ToolStripMenuItem.Name = "engList2ToolStripMenuItem";
            engList2ToolStripMenuItem.Size = new Size(148, 22);
            engList2ToolStripMenuItem.Text = "Eng List 2";
            // 
            // consistsUnitsToolStripMenuItem
            // 
            consistsUnitsToolStripMenuItem.Name = "consistsUnitsToolStripMenuItem";
            consistsUnitsToolStripMenuItem.Size = new Size(148, 22);
            consistsUnitsToolStripMenuItem.Text = "Consists Units";
            // 
            // engViewToolStripMenuItem
            // 
            engViewToolStripMenuItem.Name = "engViewToolStripMenuItem";
            engViewToolStripMenuItem.Size = new Size(148, 22);
            engViewToolStripMenuItem.Text = "Eng View";
            // 
            // conViewToolStripMenuItem
            // 
            conViewToolStripMenuItem.Name = "conViewToolStripMenuItem";
            conViewToolStripMenuItem.Size = new Size(148, 22);
            conViewToolStripMenuItem.Text = "Con View";
            // 
            // dViewToolStripMenuItem
            // 
            dViewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { shapeViewResetToolStripMenuItem, shapeViewCopyImageToolStripMenuItem, shapeViewSaveImageToolStripMenuItem, shapeViewSetColorToolStripMenuItem, conViewSetColorToolStripMenuItem });
            dViewToolStripMenuItem.Name = "dViewToolStripMenuItem";
            dViewToolStripMenuItem.Size = new Size(61, 20);
            dViewToolStripMenuItem.Text = "3D View";
            // 
            // shapeViewResetToolStripMenuItem
            // 
            shapeViewResetToolStripMenuItem.Name = "shapeViewResetToolStripMenuItem";
            shapeViewResetToolStripMenuItem.Size = new Size(204, 22);
            shapeViewResetToolStripMenuItem.Text = "Shape View: Reset";
            // 
            // shapeViewCopyImageToolStripMenuItem
            // 
            shapeViewCopyImageToolStripMenuItem.Name = "shapeViewCopyImageToolStripMenuItem";
            shapeViewCopyImageToolStripMenuItem.Size = new Size(204, 22);
            shapeViewCopyImageToolStripMenuItem.Text = "Shape View: Copy Image";
            // 
            // shapeViewSaveImageToolStripMenuItem
            // 
            shapeViewSaveImageToolStripMenuItem.Name = "shapeViewSaveImageToolStripMenuItem";
            shapeViewSaveImageToolStripMenuItem.Size = new Size(204, 22);
            shapeViewSaveImageToolStripMenuItem.Text = "Shape View: Save Image";
            // 
            // shapeViewSetColorToolStripMenuItem
            // 
            shapeViewSetColorToolStripMenuItem.Name = "shapeViewSetColorToolStripMenuItem";
            shapeViewSetColorToolStripMenuItem.Size = new Size(204, 22);
            shapeViewSetColorToolStripMenuItem.Text = "Shape View: Set Color";
            // 
            // conViewSetColorToolStripMenuItem
            // 
            conViewSetColorToolStripMenuItem.Name = "conViewSetColorToolStripMenuItem";
            conViewSetColorToolStripMenuItem.Size = new Size(204, 22);
            conViewSetColorToolStripMenuItem.Text = "Con View: Set Color";
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { autoLoadENGSetsToolStripMenuItem, refreshEngDataToolStripMenuItem, forceReloadEngDataToolStripMenuItem });
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(61, 20);
            settingsToolStripMenuItem.Text = "Settings";
            // 
            // autoLoadENGSetsToolStripMenuItem
            // 
            autoLoadENGSetsToolStripMenuItem.Name = "autoLoadENGSetsToolStripMenuItem";
            autoLoadENGSetsToolStripMenuItem.Size = new Size(192, 22);
            autoLoadENGSetsToolStripMenuItem.Text = "Auto Load ENG Sets";
            // 
            // refreshEngDataToolStripMenuItem
            // 
            refreshEngDataToolStripMenuItem.Name = "refreshEngDataToolStripMenuItem";
            refreshEngDataToolStripMenuItem.Size = new Size(192, 22);
            refreshEngDataToolStripMenuItem.Text = "Refresh Eng Data";
            // 
            // forceReloadEngDataToolStripMenuItem
            // 
            forceReloadEngDataToolStripMenuItem.Name = "forceReloadEngDataToolStripMenuItem";
            forceReloadEngDataToolStripMenuItem.Size = new Size(192, 22);
            forceReloadEngDataToolStripMenuItem.Text = "Force Reload Eng Data";
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(107, 22);
            aboutToolStripMenuItem.Text = "About";
            // 
            // listBox1
            // 
            listBox1.BackColor = Color.FromArgb(128, 255, 255);
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(0, 27);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(184, 409);
            listBox1.TabIndex = 1;
            // 
            // listBox2
            // 
            listBox2.FormattingEnabled = true;
            listBox2.ItemHeight = 15;
            listBox2.Location = new Point(366, 324);
            listBox2.Name = "listBox2";
            listBox2.Size = new Size(120, 94);
            listBox2.TabIndex = 2;
            // 
            // CEForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Control;
            ClientSize = new Size(1184, 761);
            Controls.Add(listBox2);
            Controls.Add(listBox1);
            Controls.Add(CEmenuStrip);
            MainMenuStrip = CEmenuStrip;
            MdiChildrenMinimizedAnchorBottom = false;
            Name = "CEForm";
            Text = "Consist Editor";
            CEmenuStrip.ResumeLayout(false);
            CEmenuStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip CEmenuStrip;
        private ToolStripMenuItem FileToolStripMenuItem;
        private ToolStripMenuItem NewToolStripMenuItem;
        private ToolStripMenuItem SaveToolStripMenuItem;
        private ToolStripMenuItem ExitToolStripMenuItem;
        private ToolStripMenuItem consistToolStripMenuItem;
        private ToolStripMenuItem reverseToolStripMenuItem;
        private ToolStripMenuItem cloneToolStripMenuItem;
        private ToolStripMenuItem deleteToolStripMenuItem;
        private ToolStripMenuItem openInExternalEditorToolStripMenuItem;
        private ToolStripMenuItem saveAsEngSetToolStripMenuItem;
        private ToolStripMenuItem engToolStripMenuItem;
        private ToolStripMenuItem findConsistsToolStripMenuItem;
        private ToolStripMenuItem openInExternalEditorToolStripMenuItem1;
        private ToolStripMenuItem openLegacyENGInToolStripMenuItem;
        private ToolStripMenuItem reloadEngToolStripMenuItem;
        private ToolStripMenuItem replaceToolStripMenuItem;
        private ToolStripMenuItem onlySelectedUnitToolStripMenuItem;
        private ToolStripMenuItem allUnitsInSelectedConsistsToolStripMenuItem;
        private ToolStripMenuItem allUnitsInAllConsistsToolStripMenuItem;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem consistsListsToolStripMenuItem;
        private ToolStripMenuItem engList1ToolStripMenuItem;
        private ToolStripMenuItem engList2ToolStripMenuItem;
        private ToolStripMenuItem consistsUnitsToolStripMenuItem;
        private ToolStripMenuItem engViewToolStripMenuItem;
        private ToolStripMenuItem conViewToolStripMenuItem;
        private ToolStripMenuItem dViewToolStripMenuItem;
        private ToolStripMenuItem shapeViewResetToolStripMenuItem;
        private ToolStripMenuItem shapeViewCopyImageToolStripMenuItem;
        private ToolStripMenuItem shapeViewSaveImageToolStripMenuItem;
        private ToolStripMenuItem shapeViewSetColorToolStripMenuItem;
        private ToolStripMenuItem conViewSetColorToolStripMenuItem;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem autoLoadENGSetsToolStripMenuItem;
        private ToolStripMenuItem refreshEngDataToolStripMenuItem;
        private ToolStripMenuItem forceReloadEngDataToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ListBox listBox1;
        private ListBox listBox2;
    }
}