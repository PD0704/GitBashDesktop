using CommunityToolkit.Mvvm.ComponentModel;

namespace GitBashDesktop.Models
{
    public enum Resolution { None, Ours, Theirs, Both }

    public partial class ConflictBlock : ObservableObject
    {
        [ObservableProperty] private Resolution _resolution = Resolution.None;

        public int Index { get; set; }
        public string OursText { get; set; } = "";
        public string TheirsText { get; set; } = "";

        public string DisplayIndex => $"Conflict #{Index + 1}";

        public string ResolvedText => Resolution switch
        {
            Resolution.Ours => OursText,
            Resolution.Theirs => TheirsText,
            Resolution.Both => OursText + "\n" + TheirsText,
            _ => ""
        };

        public bool IsResolved => Resolution != Resolution.None;
    }
}