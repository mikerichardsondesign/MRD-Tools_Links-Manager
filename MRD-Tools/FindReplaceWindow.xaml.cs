using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MRDTools
{
    /// <summary>
    /// Asks what to find and replace in links' paths on next open, and which links to look in. It changes nothing itself:
    /// the links manager makes the replacement once the dialog is accepted.
    /// </summary>
    public partial class FindReplaceWindow : Window
    {
        // What was last replaced, offered again the next time in this Revit session
        static string lastFind = "";
        static string lastReplacement = "";
        static bool lastMatchCase;

        readonly List<LinkRow> selected;
        readonly List<LinkRow> onTab;
        readonly List<LinkRow> every;

        /// <param name="selected">The selected Revit, CAD and DWF links.</param>
        /// <param name="onTab">Every editable link on the current tab.</param>
        /// <param name="every">Every Revit, CAD and DWF link of the projects ticked to Process.</param>
        /// <param name="tabName">The current tab, such as CAD.</param>
        public FindReplaceWindow(List<LinkRow> selected, List<LinkRow> onTab, List<LinkRow> every, string tabName)
        {
            InitializeComponent();
            Icon = WindowIcon.Load();
            this.selected = selected;
            this.onTab = onTab;
            this.every = every;

            LookInSelected.Content = $"The selected links ({selected.Count})";
            LookInSelected.IsEnabled = selected.Count > 0;
            LookInTab.Content = $"Every link on the {tabName} tab ({onTab.Count})";
            LookInAll.Content = $"Every Revit, CAD and DWF link ({every.Count})";
            (selected.Count > 0 ? LookInSelected : LookInTab).IsChecked = true;

            FindBox.Text = lastFind;
            ReplaceBox.Text = lastReplacement;
            MatchCase.IsChecked = lastMatchCase;
            Loaded += (sender, e) =>
            {
                FindBox.Focus();
                FindBox.SelectAll();
            };
            UpdatePreview();
        }

        public string FindText => FindBox.Text;
        public string ReplacementText => ReplaceBox.Text;
        public bool IsMatchCase => MatchCase.IsChecked == true;

        /// <summary>The links chosen under Look In.</summary>
        public List<LinkRow> ChosenRows => LookInSelected.IsChecked == true ? selected : LookInAll.IsChecked == true ? every : onTab;

        void Criteria_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

        /// <summary>Says how many of the chosen links' paths contain the text, and only allows Replace when some do.</summary>
        internal void UpdatePreview()
        {
            // Check boxes and radio buttons raise their events while the window is being built
            if (Preview == null || ReplaceButton == null) return;

            List<LinkRow> rows = ChosenRows;
            int matches = FindText.Length == 0 ? 0 : rows.Count(r => r.PathContains(FindText, IsMatchCase));
            Preview.Text = FindText.Length == 0 ? "Type the text to find in the links' paths."
                : matches == 0 ? "None of these paths contain it."
                : rows.Count == 1 ? "Found in the link's path."
                : $"Found in {matches} of the {rows.Count} paths.";
            ReplaceButton.IsEnabled = matches > 0;
        }

        void Replace_Click(object sender, RoutedEventArgs e)
        {
            lastFind = FindText;
            lastReplacement = ReplacementText;
            lastMatchCase = IsMatchCase;
            DialogResult = true;
        }
    }
}
